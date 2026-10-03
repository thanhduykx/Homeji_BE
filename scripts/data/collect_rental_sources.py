"""Collect a bounded set of public advertisements; never invent owners or map coordinates.

Outputs a reviewable JSON snapshot and transactional SQL. Does not connect to a DB.
Only Phongtro123's Quận 9/Thủ Đức pages and listing CDN are allowed.
"""
import argparse
import hashlib
import json
import re
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urljoin, urlparse
from urllib.robotparser import RobotFileParser

import requests
from bs4 import BeautifulSoup

BASE = "https://phongtro123.com"
CDN = "pt123.cdn.static123.com"
AGENT = "HomejiListingCollector/1.0"
DISTRICTS = ("quan-9", "thu-duc")
SOURCE_PATHS = {"quan-9": "quan-9", "thu-duc": "quan-thu-duc"}
MAX_RESPONSE_BYTES = 5 * 1024 * 1024


def fetch(session, url, allowed_host, image=False):
    parsed = urlparse(url)
    if (parsed.scheme != "https" or parsed.hostname != allowed_host
            or parsed.port not in (None, 443) or parsed.username or parsed.password):
        raise ValueError("URL outside the source allowlist")
    # Do not follow redirects into unknown hosts or private network locations.
    with session.get(url, timeout=(10, 30), allow_redirects=False, stream=True) as response:
        response.raise_for_status()
        if 300 <= response.status_code < 400:
            raise ValueError("Redirect requires review")
        mime = response.headers.get("Content-Type", "").split(";")[0].lower()
        if image:
            if mime not in ("image/jpeg", "image/png", "image/webp"):
                raise ValueError("Unsupported image response")
            first = next(response.iter_content(4096), b"")
            if not (first.startswith(b"\xff\xd8\xff") or first.startswith(b"\x89PNG\r\n\x1a\n")
                    or (first.startswith(b"RIFF") and first[8:12] == b"WEBP")):
                raise ValueError("Image signature mismatch")
            return None
        content = bytearray()
        for chunk in response.iter_content(65536):
            content.extend(chunk)
            if len(content) > MAX_RESPONSE_BYTES:
                raise ValueError("Source page exceeds size limit")
        return content.decode("utf-8")


def linked_data(soup):
    for element in soup.find_all("script", type="application/ld+json"):
        value = json.loads(element.get_text())
        if isinstance(value, dict):
            yield value


def extract_listing(html, url, district, collected_at):
    soup = BeautifulSoup(html, "html.parser")
    metadata = list(linked_data(soup))
    page = next(item for item in metadata if item.get("@type") == "WebPage")
    hostel = next(item for item in metadata if item.get("@type") == "Hostel")
    required_path = f"/tinh-thanh/ho-chi-minh/{SOURCE_PATHS[district]}"
    breadcrumbs = page.get("breadcrumb", {}).get("itemListElement", [])
    if not any(urlparse(urljoin(BASE, item.get("item", ""))).path == required_path
               for item in breadcrumbs if isinstance(item.get("item"), str)):
        raise ValueError("Listing district differs from selected district")
    address = hostel["address"]["streetAddress"].strip()
    if re.search(r"Dĩ An|Đông Hòa|Hà Nội|Thạch Thất|Bình Dương", address, re.IGNORECASE):
        raise ValueError("Address is outside the agreed geography")
    price = float(hostel["priceRange"])
    money = soup.select_one(".text-green.fs-5.fw-bold")
    if money is None:
        raise ValueError("Cannot locate the listing's own price/area block")
    area_match = re.search(r"([0-9]+(?:[.,][0-9]+)?)\s*m\s*2", money.parent.get_text(" ", strip=True))
    if not area_match:
        raise ValueError("Missing explicit area; no estimated values imported")
    area = float(area_match[1].replace(",", "."))
    source_id = re.search(r"-pr([0-9]+)\.html$", urlparse(url).path)[1]
    images = list(dict.fromkeys(img.get("src", "")
                               for img in soup.select("#carousel_Photos .carousel-inner img")))[:10]
    if not images or price <= 0 or price > 100_000_000 or area <= 0 or area > 1000:
        raise ValueError("Missing photographs or invalid numeric facts")
    title = hostel["name"].strip()
    if len(title) > 200 or len(address) > 500:
        raise ValueError("Listing exceeds API field limits")
    return {
        "id": str(uuid.uuid5(uuid.NAMESPACE_URL, url)), "source": "phongtro123",
        "source_id": source_id, "source_url": url, "title": title, "address": address,
        "district": district, "price": price, "area": area, "image_urls": images,
        "source_updated_at": page.get("dateModified"), "collected_at": collected_at,
    }


def sql_string(value):
    return "NULL" if value is None else "'" + str(value).replace("'", "''") + "'"


def render_sql(records):
    statements = ["BEGIN;", "SET LOCAL standard_conforming_strings = on;"]
    for record in records:
        values = [sql_string(record[key]) for key in (
            "id", "source", "source_id", "source_url", "title", "address", "district")]
        values += [str(record["price"]), str(record["area"]),
                   sql_string(json.dumps(record["image_urls"], ensure_ascii=False)) + "::jsonb",
                   sql_string(record["source_updated_at"]), sql_string(record["collected_at"])]
        statements.append(
            "INSERT INTO homeji.rental_source_listings "
            "(id,source,source_id,source_url,title,address,district,price,area,image_urls,source_updated_at,collected_at) "
            "VALUES (" + ",".join(values) + ") ON CONFLICT (source,source_id) DO UPDATE SET "
            "source_url=EXCLUDED.source_url,title=EXCLUDED.title,address=EXCLUDED.address,"
            "district=EXCLUDED.district,price=EXCLUDED.price,area=EXCLUDED.area,"
            "image_urls=EXCLUDED.image_urls,source_updated_at=EXCLUDED.source_updated_at,"
            "collected_at=EXCLUDED.collected_at "
            "WHERE EXCLUDED.collected_at > rental_source_listings.collected_at "
            "AND (rental_source_listings.source_updated_at IS NULL "
            "OR EXCLUDED.source_updated_at >= rental_source_listings.source_updated_at);")
    statements.append("COMMIT;")
    return "\n".join(statements) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit-per-district", type=int, default=10)
    parser.add_argument("--output", type=Path, default=Path("output/data-import"))
    args = parser.parse_args()
    if not 1 <= args.limit_per_district <= 20:
        parser.error("limit-per-district must be between 1 and 20")
    args.output.mkdir(parents=True, exist_ok=True)
    collected_at = datetime.now(timezone.utc).isoformat()
    records, failures = [], []
    with requests.Session() as session:
        session.headers["User-Agent"] = AGENT
        robots = RobotFileParser()
        robots.parse(fetch(session, BASE + "/robots.txt", "phongtro123.com").splitlines())
        for district in DISTRICTS:
            index_url = BASE + f"/tinh-thanh/ho-chi-minh/{SOURCE_PATHS[district]}?orderby=moi-nhat"
            if not robots.can_fetch(AGENT, index_url):
                raise ValueError("Source robots policy disallows the district page")
            try:
                index = BeautifulSoup(fetch(session, index_url, "phongtro123.com"), "html.parser")
            except (ValueError, requests.RequestException) as error:
                failures.append({"url": index_url, "reason": str(error)[:250]})
                print(f"Skipped district {district}: {type(error).__name__}", flush=True)
                continue
            urls = list(dict.fromkeys(item["url"] for item in linked_data(index)
                                     if item.get("@type") == "Hostel"))
            count = 0
            for url in urls:
                if count >= args.limit_per_district:
                    break
                if not robots.can_fetch(AGENT, url):
                    continue
                try:
                    time.sleep(1)
                    record = extract_listing(fetch(session, url, "phongtro123.com"), url, district, collected_at)
                    for image in record["image_urls"]:
                        time.sleep(0.2)
                        fetch(session, image, CDN, image=True)
                    records.append(record)
                    count += 1
                    print(f"Collected {district}/{record['source_id']}: {len(record['image_urls'])} images", flush=True)
                except (ValueError, KeyError, StopIteration, TypeError, requests.RequestException) as error:
                    failures.append({"url": url, "reason": str(error)[:250]})
                    print(f"Skipped {url}: {type(error).__name__}", flush=True)
    payload = json.dumps({"collected_at": collected_at, "records": records, "skipped": failures}, ensure_ascii=False, indent=2)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    snapshot = args.output / f"rental-sources-{stamp}.json"
    snapshot.write_text(payload, encoding="utf-8")
    sql_path = snapshot.with_suffix(".sql")
    sql_path.write_text(render_sql(records), encoding="utf-8")
    digest = hashlib.sha256(payload.encode()).hexdigest()
    print(json.dumps({"snapshot": str(snapshot), "sql": str(sql_path), "records": len(records),
                      "images": sum(len(x["image_urls"]) for x in records), "sha256": digest}))
    if not records:
        raise SystemExit("No valid records collected; database import must not proceed.")


if __name__ == "__main__":
    main()
