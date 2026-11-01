"""Collect bounded, attributed Quận 9 advertisements from public HTML and source galleries."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
from io import BytesIO
import json
from pathlib import Path
import re
import time
import unicodedata
from urllib.parse import urljoin, urlparse
from urllib.robotparser import RobotFileParser
import uuid

from bs4 import BeautifulSoup
from PIL import Image
import requests

from collect_rental_sources import AGENT, fetch, extract_listing, linked_data

SOURCES = {
    'phongtro123': {
        'host': 'phongtro123.com', 'images': 'pt123.cdn.static123.com',
        'index': 'https://phongtro123.com/tinh-thanh/ho-chi-minh/quan-9?orderby=moi-nhat',
        'priority': 'https://phongtro123.com/tags/phong-tro-duong-le-van-viet-quan-9.html',
        'detail': r'-pr\d+\.html$', 'page': 'page',
    },
    'mogi': {
        'host': 'mogi.vn', 'images': 'cloud.mogi.vn',
        'index': 'https://mogi.vn/ho-chi-minh/quan-9/thue-phong-tro-nha-tro',
        'priority': 'https://mogi.vn/ho-chi-minh/quan-9/thue-phong-tro-nha-tro/duong-le-van-viet-quan-9-sid8486',
        'detail': r'-id\d+$', 'page': 'cp',
    },
    'phongtrovn': {
        'host': 'phongtro.vn', 'images': 'file.phongtro.vn',
        'index': 'https://phongtro.vn/cho-thue-phong-tro-phuong-long-phuoc-tp-ho-chi-minh',
        'priority': 'https://phongtro.vn/cho-thue-phong-tro-phuong-long-phuoc-tp-ho-chi-minh',
        'detail': r'/cho-thue-phong-tro[^?]*/\d+$', 'page': 'page',
    },
}


def normalize(value):
    return re.sub(r'[^a-z0-9]+', ' ', ''.join(c for c in unicodedata.normalize('NFD', value.lower().replace('đ', 'd'))
                                           if unicodedata.category(c) != 'Mn')).strip()


def extract_mogi(html, url, collected_at):
    soup = BeautifulSoup(html, 'html.parser')
    offers = [value.get('makesOffer') for value in linked_data(soup) if value.get('makesOffer')]
    if len(offers) != 1:
        raise ValueError('Missing listing-specific structured offer')
    offer = offers[0]
    house = offer['itemOffered']
    address = house['address'].strip()
    if not re.search(r'\bQuận 9\b', address, re.I) or '/quan-9/' not in urlparse(url).path:
        raise ValueError('Listing does not explicitly confirm Quận 9')
    price = float(offer['priceSpecification']['price'])
    area_match = re.fullmatch(r'\s*([\d.,]+)\s*m[²2]\s*', str(house['floorSize']))
    if offer['priceSpecification'].get('priceCurrency') != 'VND' or not area_match:
        raise ValueError('Missing price currency or explicit area')
    area = float(area_match[1].replace(',', '.'))
    title = house['name'].strip()
    if not 0 < price <= 100_000_000 or not 0 < area <= 1000 or len(title) > 200 or len(address) > 500:
        raise ValueError('Numeric facts or field lengths outside limits')
    images = list(dict.fromkeys(img.get('src') or img.get('data-src') for img in soup.select('.media-item img')))
    if not images:
        raise ValueError('No source gallery')
    dates = []
    for element in soup.select('.info-attr'):
        text = element.get_text(' ', strip=True)
        if 'Ngày đăng' in text:
            match = re.search(r'\b(\d{2}/\d{2}/\d{4})\b', text)
            if match:
                dates.append(datetime.strptime(match[1], '%d/%m/%Y').replace(tzinfo=timezone.utc).isoformat())
    return {'id': str(uuid.uuid5(uuid.NAMESPACE_URL, url)), 'source': 'mogi',
            'source_id': re.search(r'-id(\d+)$', url)[1], 'source_url': url,
            'title': title, 'address': address, 'district': 'quan-9', 'price': price, 'area': area,
            'image_urls': images[:10], 'source_updated_at': dates[0] if dates else None,
            'collected_at': collected_at, 'source_checked_at': collected_at, 'source_expires_at': None}


def extract_phongtrovn(html, url, collected_at):
    soup = BeautifulSoup(html, 'html.parser')
    listing = next(item for item in linked_data(soup) if item.get('@type') == 'RealEstateListing')
    address = listing['about']['address']
    if listing['url'] != url or normalize(address['addressRegion']) != 'ho chi minh' or normalize(address['addressLocality']) != 'long phuoc':
        raise ValueError('Source listing does not confirm the selected Long Phước area')
    offer = listing['offers']
    if offer.get('priceCurrency') != 'VND' or offer.get('availability') == 'https://schema.org/OutOfStock':
        raise ValueError('Unsupported currency or unavailable advertisement')
    area = listing['about']['floorSize']
    if area.get('unitCode') != 'MTK': raise ValueError('Area unit is not square metres')
    label = soup.find(string=re.compile(r'^Ngày hết hạn$'))
    expiry = label.parent.parent.select_one('time[datetime]')['datetime'] if label else None
    if not expiry: raise ValueError('Missing source expiry')
    return {'id': str(uuid.uuid5(uuid.NAMESPACE_URL, url)), 'source': 'phongtrovn',
            'source_id': url.rsplit('/', 1)[1], 'source_url': url, 'title': listing['name'].strip(),
            'address': ', '.join([address['streetAddress'], 'Phường ' + address['addressLocality'], address['addressRegion']]),
            'district': 'quan-9', 'price': float(offer['price']), 'area': float(area['value']),
            'image_urls': list(dict.fromkeys(listing['image']))[:10], 'source_updated_at': listing.get('datePosted'),
            'collected_at': collected_at, 'source_checked_at': collected_at, 'source_expires_at': expiry}


def check_photo(session, url, host):
    parsed = urlparse(url)
    if parsed.scheme != 'https' or parsed.hostname != host or parsed.username or parsed.password or parsed.port not in (None, 443):
        raise ValueError('Image URL outside gallery allowlist')
    with session.get(url, timeout=(10, 25), stream=True, allow_redirects=False) as response:
        response.raise_for_status()
        if response.status_code != 200 or response.headers.get('Content-Type', '').split(';')[0] not in ('image/jpeg', 'image/png', 'image/webp'):
            raise ValueError('Unsupported image response')
        content = bytearray()
        for chunk in response.iter_content(65536):
            content.extend(chunk)
            if len(content) > 8 * 1024 * 1024:
                raise ValueError('Image exceeds 8 MB')
    with Image.open(BytesIO(content)) as image:
        width, height = image.size
        if image.format not in ('JPEG', 'PNG', 'WEBP') or min(width, height) < 100 or width * height > 40_000_000:
            raise ValueError('Invalid gallery image dimensions')
        image.verify()
    return {'url': url, 'width': width, 'height': height, 'sha256': hashlib.sha256(content).hexdigest()}


def collect(source, limit, pages, output):
    config = SOURCES[source]
    base = 'https://' + config['host']
    collected_at = datetime.now(timezone.utc).isoformat()
    records, rejected, photo_checks = [], [], {}
    with requests.Session() as session:
        session.headers['User-Agent'] = AGENT
        robots_text = fetch(session, base + '/robots.txt', config['host'])
        (output / f'{source}-robots.txt').write_text(robots_text, encoding='utf-8')
        robots = RobotFileParser(); robots.parse(robots_text.splitlines())
        indexes = [config['priority'], config['index']]
        separator = '&' if '?' in config['index'] else '?'
        indexes += [config['index'] + separator + config['page'] + '=' + str(page) for page in range(2, pages + 1)]
        queue = []
        seen = set()
        for index in indexes:
            if not robots.can_fetch(AGENT, index):
                rejected.append({'url': index, 'reason': 'robots_disallowed'}); continue
            try:
                time.sleep(1)
                soup = BeautifulSoup(fetch(session, index, config['host']), 'html.parser')
                for anchor in soup.select('a[href]'):
                    url = urljoin(base, anchor['href']).split('#')[0].split('?')[0]
                    if urlparse(url).hostname == config['host'] and re.search(config['detail'], url) and url not in seen:
                        seen.add(url); queue.append(url)
            except (ValueError, requests.RequestException) as error:
                rejected.append({'url': index, 'reason': type(error).__name__})
        print(json.dumps({'source': source, 'discovered': len(queue)}), flush=True)
        for url in queue:
            if len(records) >= limit: break
            if not robots.can_fetch(AGENT, url): continue
            try:
                time.sleep(1)
                html = fetch(session, url, config['host'])
                record = (extract_listing(html, url, 'quan-9', collected_at) if source == 'phongtro123'
                          else extract_mogi(html, url, collected_at) if source == 'mogi' else extract_phongtrovn(html, url, collected_at))
                expiry = record.get('source_expires_at')
                if expiry and datetime.fromisoformat(expiry) <= datetime.now(timezone.utc):
                    raise ValueError('Source advertisement has expired')
                soup = BeautifulSoup(html, 'html.parser')
                if soup.select_one('#da_het_han') or re.search(r'\b(?:đã cho thuê|đã hết hạn)\b', soup.select_one('h1').get_text(' ', strip=True), re.I):
                    raise ValueError('Source marks advertisement unavailable')
                verified = []
                for image in record['image_urls'][:5]:
                    try:
                        if image not in photo_checks:
                            time.sleep(0.15)
                            photo_checks[image] = check_photo(session, image, config['images'])
                        verified.append(image)
                    except (ValueError, OSError, requests.RequestException): pass
                if not verified: raise ValueError('No gallery image passed download/decoding validation')
                record['image_urls'] = verified
                record['availability_verified'] = False
                record['rights_status'] = 'not_confirmed'
                records.append(record)
                print(json.dumps({'source': source, 'accepted': len(records), 'source_id': record['source_id'], 'images': len(verified)}), flush=True)
            except (ValueError, KeyError, TypeError, StopIteration, AttributeError, requests.RequestException) as error:
                rejected.append({'url': url, 'reason': str(error)[:200]})
            (output / f'{source}-snapshot.json').write_text(json.dumps({'records': records, 'rejected': rejected}, ensure_ascii=False, indent=2), encoding='utf-8')
    (output / f'{source}-photos.json').write_text(json.dumps(list(photo_checks.values()), indent=2), encoding='utf-8')
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--per-source', type=int, default=100)
    parser.add_argument('--pages', type=int, default=8)
    parser.add_argument('--output', type=Path, default=Path('output/data-import/quan9-20261101'))
    args = parser.parse_args()
    if not 1 <= args.per_source <= 150 or not 1 <= args.pages <= 15: parser.error('Bounds: 1–150 records/source, 1–15 index pages')
    args.output.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(collect, source, args.per_source, args.pages, args.output) for source in SOURCES]
        records = [record for future in futures for record in future.result()]
    seen, unique, duplicates = set(), [], []
    for record in records:
        key = (normalize(record['title']), normalize(record['address']), record['price'], record['area'])
        if key in seen: duplicates.append(record['source_url']); continue
        seen.add(key); unique.append(record)
    (args.output / 'batch.json').write_text(json.dumps({'records': unique, 'duplicate_urls': duplicates, 'availability_verified': False}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'batch': str(args.output / 'batch.json'), 'records': len(unique), 'duplicates': len(duplicates)}), flush=True)


if __name__ == '__main__': main()
