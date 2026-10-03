"""Continue the primary source from observed pagination without recollecting a batch."""
import argparse
import json
import re
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.robotparser import RobotFileParser

import requests
from bs4 import BeautifulSoup
from collect_rental_sources import AGENT, BASE, CDN, SOURCE_PATHS, extract_listing, fetch, linked_data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--existing", type=Path, required=True)
    parser.add_argument("--limit", type=int, default=21)
    parser.add_argument("--output", type=Path, default=Path("output/data-import"))
    args = parser.parse_args()
    if not 1 <= args.limit <= 50:
        parser.error("limit must be between 1 and 50")
    existing = json.loads(args.existing.read_text(encoding="utf-8"))["records"]
    seen = {record["source_url"] for record in existing}
    records, skipped = [], []
    collected_at = datetime.now(timezone.utc).isoformat()
    args.output.mkdir(parents=True, exist_ok=True)
    snapshot = args.output / ("additional-rental-sources-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + ".json")
    with requests.Session() as session:
        session.headers["User-Agent"] = AGENT
        robots = RobotFileParser()
        robots.parse(fetch(session, BASE + "/robots.txt", "phongtro123.com").splitlines())
        for district, path in SOURCE_PATHS.items():
            index_url = BASE + f"/tinh-thanh/ho-chi-minh/{path}?orderby=moi-nhat"
            for page in range(4):
                if not robots.can_fetch(AGENT, index_url):
                    raise ValueError("Robots policy disallows index")
                time.sleep(1)
                index = BeautifulSoup(fetch(session, index_url, "phongtro123.com"), "html.parser")
                urls = list(dict.fromkeys(item["url"] for item in linked_data(index) if item.get("@type") == "Hostel"))
                for url in urls:
                    if len(records) >= args.limit:
                        break
                    if url in seen or not robots.can_fetch(AGENT, url):
                        continue
                    seen.add(url)
                    try:
                        time.sleep(1)
                        record = extract_listing(fetch(session, url, "phongtro123.com"), url, district, collected_at)
                        for image in record["image_urls"]:
                            time.sleep(0.2)
                            fetch(session, image, CDN, image=True)
                        records.append(record)
                        print(f"Collected {district}/{record['source_id']}", flush=True)
                    except (ValueError, KeyError, StopIteration, TypeError, requests.RequestException) as error:
                        skipped.append({"url": url, "reason": str(error)[:250]})
                        print(f"Skipped {url}: {type(error).__name__}", flush=True)
                    snapshot.write_text(json.dumps({"records": records, "skipped": skipped}, ensure_ascii=False, indent=2), encoding="utf-8")
                if len(records) >= args.limit:
                    break
                next_link = next((a["href"] for a in index.select("a[href]")
                                  if "Trang sau" in a.get_text() and re.search(r"[?&]page=\d+", a["href"])), None)
                if next_link is None:
                    break
                index_url = next_link
            if len(records) >= args.limit:
                break
    print(json.dumps({"snapshot": str(snapshot), "records": len(records), "images": sum(len(x["image_urls"]) for x in records)}))
    if not records:
        raise SystemExit("No additional valid records collected")


if __name__ == "__main__":
    main()
