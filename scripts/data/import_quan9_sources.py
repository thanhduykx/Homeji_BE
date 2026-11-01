"""Validate a source-only batch and append it transactionally to the existing Homeji catalog."""
import argparse
from collections import Counter
from datetime import datetime, timezone
from decimal import Decimal
import json
from pathlib import Path
import re
from urllib.parse import urlparse
import uuid

import psycopg
from psycopg.types.json import Jsonb
from collect_quan9_sources import SOURCES, normalize


def validate(records):
    if len(records) < 100:
        raise ValueError('At least 100 collected records are required')
    seen, unique, excluded = set(), [], []
    now = datetime.now(timezone.utc)
    for item in records:
        source = item['source']; config = SOURCES[source]
        uuid.UUID(item['id'])
        url = urlparse(item['source_url'])
        if url.scheme != 'https' or url.hostname != config['host'] or url.username or url.password or url.port not in (None, 443):
            raise ValueError('Source URL outside allowlist')
        if not re.fullmatch(r'[0-9]{1,80}', item['source_id']) or not re.search(config['detail'], url.path):
            raise ValueError('Invalid source identity')
        if source == 'mogi' and not url.path.startswith('/quan-9/thue-phong-tro-'):
            raise ValueError('Mogi record is not a Quận 9 room rental')
        if item['district'] != 'quan-9' or not 0 < len(item['title'].strip()) <= 200 or not 0 < len(item['address'].strip()) <= 500:
            raise ValueError('Invalid geography or empty/oversized fields')
        for field, ceiling in [('price', 100_000_000), ('area', 1000)]:
            number = Decimal(str(item[field]))
            if not number.is_finite() or not 0 < number <= ceiling:
                raise ValueError('Invalid numeric facts')
        if item.get('availability_verified') is not False:
            raise ValueError('Source imports cannot verify actual availability')
        if not 1 <= len(item['image_urls']) <= 10:
            raise ValueError('Every record needs a gallery')
        for image in item['image_urls']:
            photo = urlparse(image)
            if photo.scheme != 'https' or photo.hostname != config['images'] or photo.username or photo.password or photo.port not in (None, 443) or len(image) > 1000:
                raise ValueError('Image outside allowlist')
        for field in ['collected_at', 'source_checked_at']:
            value = datetime.fromisoformat(item[field])
            if value.tzinfo is None or value > now.replace(microsecond=999999):
                raise ValueError('Invalid collection/check timestamp')
        expiry = item.get('source_expires_at')
        if expiry and datetime.fromisoformat(expiry) <= now:
            excluded.append({'source': source, 'source_id': item['source_id'], 'reason': 'expired'}); continue
        keys = [('identity', source, item['source_id']), ('url', item['source_url']),
                ('facts', normalize(item['title']), normalize(item['address']), str(item['price']), str(item['area']))]
        if any(key in seen for key in keys):
            excluded.append({'source': source, 'source_id': item['source_id'], 'reason': 'duplicate'}); continue
        seen.update(keys); unique.append(item)
    if len(unique) < 100 or len({item['source'] for item in unique}) < 2:
        raise ValueError('Need at least 100 unique, unexpired records from multiple sources')
    return unique, excluded


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('batch', type=Path)
    parser.add_argument('--settings', type=Path, default=Path(__file__).resolve().parents[2] / 'src/Homeji.Api/appsettings.Local.json')
    parser.add_argument('--expected-project', required=True)
    parser.add_argument('--receipt', type=Path, required=True)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    records, excluded = validate(json.loads(args.batch.read_text(encoding='utf-8'))['records'])
    connection = json.loads(args.settings.read_text(encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
    settings = {key.strip().lower(): value for key, value in (part.split('=', 1) for part in connection.split(';') if '=' in part)}
    if settings.get('database') != 'postgres' or not settings.get('username', '').endswith('.' + args.expected_project) or not settings.get('host', '').endswith('.pooler.supabase.com'):
        raise ValueError('Database identity does not match the explicitly selected project')
    with psycopg.connect(host=settings['host'], port=settings.get('port', '5432'), dbname=settings['database'], user=settings['username'], password=settings['password'], sslmode='require', connect_timeout=20) as db:
        db.execute("SET LOCAL lock_timeout = '5s'")
        db.execute("SET LOCAL statement_timeout = '30s'")
        existing = set(db.execute('SELECT source,source_id FROM homeji.rental_source_listings').fetchall())
        additions = [item for item in records if (item['source'], item['source_id']) not in existing]
        if len(additions) < 100:
            raise ValueError(f'Only {len(additions)} new records; collect more before importing')
        inserted, refreshed = [], []
        if args.apply:
            for item in additions:
                row = db.execute('''INSERT INTO homeji.rental_source_listings
                    (id,source,source_id,source_url,title,address,district,price,area,image_urls,
                     source_updated_at,collected_at,source_expires_at,source_checked_at)
                    VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
                    ON CONFLICT (source,source_id) DO NOTHING RETURNING id''',
                    [item['id'], item['source'], item['source_id'], item['source_url'], item['title'], item['address'], 'quan-9',
                     Decimal(str(item['price'])), Decimal(str(item['area'])), Jsonb(item['image_urls']),
                     item.get('source_updated_at'), item['collected_at'], item.get('source_expires_at'), item['source_checked_at']]).fetchone()
                if row: inserted.append(str(row[0]))
            if len(inserted) < 100: raise ValueError('Concurrent imports reduced new additions below 100; transaction rolled back')
            for item in records:
                if (item['source'], item['source_id']) not in existing: continue
                row = db.execute('''UPDATE homeji.rental_source_listings SET
                    source_url=%s,title=%s,address=%s,price=%s,area=%s,image_urls=%s,
                    source_updated_at=%s,collected_at=%s,source_expires_at=%s,source_checked_at=%s
                    WHERE source=%s AND source_id=%s AND district='quan-9' AND collected_at<%s
                    AND (source_updated_at IS NULL OR source_updated_at<=%s) RETURNING id''',
                    [item['source_url'], item['title'], item['address'], Decimal(str(item['price'])), Decimal(str(item['area'])),
                     Jsonb(item['image_urls']), item.get('source_updated_at'), item['collected_at'], item.get('source_expires_at'),
                     item['source_checked_at'], item['source'], item['source_id'], item['collected_at'], item.get('source_updated_at')]).fetchone()
                if row: refreshed.append(str(row[0]))
            count = db.execute('SELECT count(*) FROM homeji.rental_source_listings WHERE id = ANY(%s::uuid[]) AND district=%s AND jsonb_array_length(image_urls)>0', [inserted, 'quan-9']).fetchone()[0]
            if count != len(inserted): raise ValueError('Readback mismatch; transaction rolled back')
            db.commit()
        else: db.rollback()
        receipt = {'status': 'committed' if args.apply else 'dry_run', 'checked_at': datetime.now(timezone.utc).isoformat(),
                   'project': args.expected_project, 'unique_collected': len(records), 'new_records': len(additions),
                   'inserted_count': len(inserted), 'inserted_ids': inserted, 'existing_in_batch': len(records) - len(additions),
                   'preserved_existing': len(records) - len(additions) - len(refreshed),
                   'refreshed_count': len(refreshed), 'refreshed_ids': refreshed,
                   'collected_sources': dict(Counter(item['source'] for item in records)),
                   'sources': dict(Counter(item['source'] for item in additions)),
                   'image_urls': sum(len(item['image_urls']) for item in additions), 'excluded': excluded,
                   'availability_verified': False, 'image_storage': 'Original source gallery URLs; no re-upload'}
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        args.receipt.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps({key: value for key, value in receipt.items() if key not in ['inserted_ids', 'refreshed_ids', 'excluded']}))


if __name__ == '__main__': main()
