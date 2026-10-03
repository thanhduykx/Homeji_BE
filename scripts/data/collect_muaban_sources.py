"""Collect public, non-expired Muaban room ads without copying contacts/descriptions."""
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

HOST = "muaban.net"
BASE = "https://muaban.net"
IMAGE_HOST = "cloud.muaban.net"
PATHS = {"quan-9": "/bat-dong-san/cho-thue-nha-tro-phong-tro-quan-9-ho-chi-minh",
         "thu-duc": "/bat-dong-san/cho-thue-nha-tro-phong-tro-quan-thu-duc-ho-chi-minh"}


def page_data(html):
    element = BeautifulSoup(html, "html.parser").select_one("#__NEXT_DATA__")
    if element is None:
        raise ValueError("Missing public page data")
    return json.loads(element.get_text())["props"]["pageProps"]


def extract_muaban_listing(html, url, district, collected_at):
    listing = page_data(html)["classified"]
    expected_id = re.search(r"-id(\d+)$", urlparse(url).path)[1]
    if str(listing["id"]) != expected_id or listing["city_id"] != 30:
        raise ValueError("Page identity or city mismatch")
    if listing["is_expired"] or datetime.fromisoformat(listing["service_end"]) <= datetime.now(timezone.utc):
        raise ValueError("Source advertisement expired")
    if not urlparse(url).path.startswith(PATHS[district] + "/"):
        raise ValueError("Wrong source district")
    title, address = listing["title"].strip(), listing["address"].strip()
    if not re.search(r"Hồ Chí Minh", address, re.I) or re.search(r"Dĩ An|Bình Dương|Quận 2\b", address, re.I):
        raise ValueError("Address outside agreed geography")
    if district == "thu-duc" and "Thủ Đức" not in address:
        raise ValueError("Address does not confirm Thu Duc")
    if district == "quan-9" and not re.search(r"Quận 9|Thủ Đức", address):
        raise ValueError("Address does not confirm District 9")
    areas = [re.fullmatch(r"([\d.,]+)\s*m²", attribute["value"]) for attribute in listing["attributes"]]
    areas = [float(match[1].replace(",", ".")) for match in areas if match]
    if len(areas) != 1:
        raise ValueError("No unambiguous explicit area")
    price, area = float(listing["price"]), areas[0]
    images = list(dict.fromkeys(image["url"] for image in listing["images"]))[:10]
    if not 0 < price <= 100_000_000 or not 0 < area <= 1000 or not images or len(title) > 200 or len(address) > 500:
        raise ValueError("Invalid facts or missing own gallery")
    for image in images:
        parsed = urlparse(image)
        if parsed.scheme != "https" or parsed.hostname != IMAGE_HOST or parsed.username or parsed.password or parsed.port not in (None, 443):
            raise ValueError("Image outside allowlist")
    return {"id": str(uuid.uuid5(uuid.NAMESPACE_URL, url)), "source": "muaban",
            "source_id": expected_id, "source_url": url, "title": title, "address": address,
            "district": district, "price": price, "area": area, "image_urls": images,
            "source_updated_at": listing["publish_at"], "collected_at": collected_at,
            "source_status": "unconfirmed", "source_expires_at": listing["service_end"],
            "rights_status": "not_confirmed", "availability_verified": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit", type=int, default=62)
    parser.add_argument("--output", type=Path, default=Path("output/data-import"))
    args = parser.parse_args()
    if not 1 <= args.limit <= 100:
        parser.error("limit must be between 1 and 100")
    records, skipped, seen = [], [], set()
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    collected_at = datetime.now(timezone.utc).isoformat()
    args.output.mkdir(parents=True, exist_ok=True)
    snapshot = args.output / f"muaban-rental-sources-{stamp}.json"
    with requests.Session() as session:
        session.headers["User-Agent"] = AGENT
        robots = RobotFileParser()
        robots.parse(fetch(session, BASE + "/robots.txt", HOST).splitlines())
        for district, path in PATHS.items():
            for page in range(1, 7):
                index_url = BASE + path + (f"?page={page}" if page > 1 else "")
                if not robots.can_fetch(AGENT, index_url):
                    raise ValueError("Robots policy disallows index")
                time.sleep(1)
                index = BeautifulSoup(fetch(session, index_url, HOST), "html.parser")
                urls = list(dict.fromkeys(urljoin(BASE, a["href"]) for a in index.select("a[href]")
                                         if a["href"].startswith(path + "/") and re.search(r"-id\d+$", a["href"])))
                new_urls = [url for url in urls if url not in seen]
                if not new_urls:
                    break
                for url in new_urls:
                    if len(records) >= args.limit:
                        break
                    seen.add(url)
                    if not robots.can_fetch(AGENT, url):
                        continue
                    try:
                        time.sleep(1)
                        record = extract_muaban_listing(fetch(session, url, HOST), url, district, collected_at)
                        for image in record["image_urls"]:
                            time.sleep(0.2)
                            fetch(session, image, IMAGE_HOST, image=True)
                        records.append(record)
                        print(f"Collected {district}/{record['source_id']}: {len(record['image_urls'])} images", flush=True)
                    except (ValueError, KeyError, TypeError, requests.RequestException) as error:
                        skipped.append({"url": url, "reason": str(error)[:250]})
                        print(f"Skipped {url}: {type(error).__name__}", flush=True)
                    snapshot.write_text(json.dumps({"records": records, "skipped": skipped}, ensure_ascii=False, indent=2), encoding="utf-8")
                if len(records) >= args.limit:
                    break
            if len(records) >= args.limit:
                break
    print(json.dumps({"snapshot": str(snapshot), "records": len(records), "images": sum(len(x["image_urls"]) for x in records)}))
    if not records:
        raise SystemExit("No valid records collected")


if __name__ == "__main__":
    main()
