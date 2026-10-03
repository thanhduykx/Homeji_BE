"""Collect Thuephongtro advertisements as source evidence, not verified availability.

Stores facts and original image URLs only; never copies descriptions or contact data.
Expired advertisements are explicitly marked and must not be sold as available rooms.
"""
import argparse
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
from collect_rental_sources import AGENT, fetch

BASE = "https://thuephongtro.com"
HOST = "thuephongtro.com"
IMAGE_HOST = "img.thuephongtro.com"
PATHS = {"quan-9": "cho-thue-phong-tro-quan-9", "thu-duc": "cho-thue-phong-tro-thu-duc"}


def extract_secondary_listing(html, url, district, collected_at):
    soup = BeautifulSoup(html, "html.parser")
    overview = soup.select_one("#overview")
    if overview is None or not overview.select_one(f'a[href="{PATHS[district]}"]'):
        raise ValueError("Source does not confirm the agreed district")
    title = overview.select_one("h1").get_text(" ", strip=True)
    address = overview.select_one(".post-address span span").get_text(" ", strip=True)
    if not re.search(r"Hồ Chí Minh", address, re.I) or re.search(r"Dĩ An|Bình Dương|Quận 2", address, re.I):
        raise ValueError("Address outside the agreed geography")
    price_text = overview.select_one(".post-price").get_text(" ", strip=True)
    price_match = re.fullmatch(r"([\d.,]+)\s*(Triệu|Nghìn)/tháng", price_text, re.I)
    area_match = re.fullmatch(r"([\d.,]+)\s*m²", overview.select_one(".post-acreage").get_text(" ", strip=True))
    if not price_match or not area_match:
        raise ValueError("Missing explicit monthly price or area")
    price = float(price_match[1].replace(",", ".")) * (1_000_000 if price_match[2].lower() == "triệu" else 1_000)
    area = float(area_match[1].replace(",", "."))
    images = list(dict.fromkeys(image["src"] for image in overview.select(".post-images img[src]")))[:10]
    if not 0 < price <= 100_000_000 or not 0 < area <= 1000 or not images:
        raise ValueError("Invalid facts or missing gallery")
    if len(title) > 200 or len(address) > 500:
        raise ValueError("Field exceeds database limits")
    for image in images:
        parsed = urlparse(image)
        if parsed.scheme != "https" or parsed.hostname != IMAGE_HOST or parsed.username or parsed.password or parsed.port not in (None, 443):
            raise ValueError("Gallery URL outside allowlist")
    updated = datetime.strptime(overview.select_one(".post-published span").get_text(strip=True), "%d/%m/%Y").replace(tzinfo=timezone.utc)
    return {"id": str(uuid.uuid5(uuid.NAMESPACE_URL, url)), "source": "thuephongtro",
            "source_id": re.search(r"-(\d+)\.html$", urlparse(url).path)[1], "source_url": url,
            "title": title, "address": address, "district": district, "price": price, "area": area,
            "image_urls": images, "source_updated_at": updated.isoformat(), "collected_at": collected_at,
            "source_status": "expired" if overview.select_one("#da_het_han") is not None else "unconfirmed",
            "rights_status": "not_confirmed", "availability_verified": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit-per-district", type=int, default=30)
    parser.add_argument("--output", type=Path, default=Path("output/data-import"))
    args = parser.parse_args()
    if not 1 <= args.limit_per_district <= 50:
        parser.error("limit-per-district must be between 1 and 50")
    records, skipped, seen = [], [], set()
    collected_at = datetime.now(timezone.utc).isoformat()
    with requests.Session() as session:
        session.headers["User-Agent"] = AGENT
        robots = RobotFileParser()
        robots.parse(fetch(session, BASE + "/robots.txt", HOST).splitlines())
        for district, path in PATHS.items():
            count = 0
            for page in range(1, 6):
                index_url = f"{BASE}/{path}" + (f"?page={page}" if page > 1 else "")
                if not robots.can_fetch(AGENT, index_url):
                    raise ValueError("Robots policy disallows index")
                time.sleep(1)
                index = BeautifulSoup(fetch(session, index_url, HOST), "html.parser")
                urls = list(dict.fromkeys(urljoin(BASE, a["href"]) for a in index.select("a[href]")
                                         if re.search(r"-\d+\.html$", a["href"])))
                for url in urls:
                    if count >= args.limit_per_district:
                        break
                    if url in seen or not robots.can_fetch(AGENT, url):
                        continue
                    seen.add(url)
                    try:
                        time.sleep(1)
                        record = extract_secondary_listing(fetch(session, url, HOST), url, district, collected_at)
                        for image in record["image_urls"]:
                            time.sleep(0.2)
                            fetch(session, image, IMAGE_HOST, image=True)
                        records.append(record)
                        count += 1
                        print(f"Collected {district}/{record['source_id']} ({record['source_status']})", flush=True)
                    except (ValueError, AttributeError, KeyError, TypeError, requests.RequestException) as error:
                        skipped.append({"url": url, "reason": str(error)[:250]})
                        print(f"Skipped {url}: {type(error).__name__}", flush=True)
                if count >= args.limit_per_district:
                    break
    args.output.mkdir(parents=True, exist_ok=True)
    path = args.output / ("secondary-rental-sources-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + ".json")
    path.write_text(json.dumps({"records": records, "skipped": skipped}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"snapshot": str(path), "records": len(records), "images": sum(len(x["image_urls"]) for x in records)}))
    if not records:
        raise SystemExit("No valid source evidence collected")


if __name__ == "__main__":
    main()
