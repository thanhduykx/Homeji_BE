"""Refresh source facts and collect Quận 9 ads. Produces evidence only; never writes a DB."""
import argparse
import json
import re
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urljoin, urlparse
from urllib.robotparser import RobotFileParser

import requests
from bs4 import BeautifulSoup
from collect_rental_sources import AGENT, BASE, CDN, extract_listing, fetch, linked_data
from check_rental_source_expiry import checked_expiry, source_identity


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path, help="DB rental_source_listings.json backup")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--new-limit", type=int, default=20)
    args = parser.parse_args()
    if not 0 <= args.new_limit <= 50:
        parser.error("new-limit must be 0–50")
    prior = json.loads(args.snapshot.read_text(encoding="utf-8-sig"))
    records, expiry_updates, failures, seen = [], [], [], set()
    evidence = {"records": records, "expiry_updates": expiry_updates, "failures": failures,
                "availability_verified": False, "rights_status": "not_confirmed"}
    args.output.parent.mkdir(parents=True, exist_ok=True)

    def save():
        args.output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")

    with requests.Session() as session:
        session.headers["User-Agent"] = AGENT
        robots = {}

        def permitted(url):
            host = urlparse(url).hostname
            if host not in ("phongtro123.com", "muaban.net"):
                return False
            if host not in robots:
                policy = RobotFileParser()
                policy.parse(fetch(session, f"https://{host}/robots.txt", host).splitlines())
                robots[host] = policy
            return robots[host].can_fetch(AGENT, url)

        def collect(url, district):
            seen.add(url)
            try:
                if not source_identity(url) or not permitted(url):
                    raise ValueError("Source URL or robots policy disallows collection")
                time.sleep(1)
                html = fetch(session, url, urlparse(url).hostname)
                now = datetime.now(timezone.utc).isoformat()
                expiry = checked_expiry(html, url)
                expiry_updates.append({"source_url": url, "source_expires_at": expiry, "source_checked_at": now})
                if urlparse(url).hostname == "phongtro123.com":
                    record = extract_listing(html, url, district, now)
                    if not expiry or datetime.fromisoformat(expiry) <= datetime.now(timezone.utc):
                        print(f"Expired {source_identity(url)}", flush=True)
                        return False
                    # Validate the advertisement's own cover, retain its original gallery URLs.
                    fetch(session, record["image_urls"][0], CDN, image=True)
                    records.append(record)
                print(f"Checked {source_identity(url)}", flush=True)
                return True
            except (ValueError, KeyError, StopIteration, TypeError, requests.RequestException) as error:
                failures.append({"source_url": url, "reason": type(error).__name__})
                print(f"Skipped {source_identity(url)}: {type(error).__name__}", flush=True)
                if isinstance(error, requests.HTTPError) and error.response.status_code == 429:
                    raise SystemExit("Rate limited; snapshot saved, stop collection") from error
                return False
            finally:
                save()

        # Refresh existing facts/expiry first, including the separate Thủ Đức catalog.
        for item in prior:
            collect(item["source_url"], item["district"])

        new_count = 0
        index_url = BASE + "/tinh-thanh/ho-chi-minh/quan-9?orderby=moi-nhat"
        for _ in range(4):
            if new_count >= args.new_limit or not permitted(index_url):
                break
            time.sleep(1)
            index = BeautifulSoup(fetch(session, index_url, "phongtro123.com"), "html.parser")
            urls = list(dict.fromkeys(item["url"] for item in linked_data(index) if item.get("@type") == "Hostel"))
            for url in urls:
                if new_count >= args.new_limit:
                    break
                if url not in seen and collect(url, "quan-9"):
                    new_count += 1
            next_link = next((a["href"] for a in index.select("a[href]")
                              if "Trang sau" in a.get_text() and re.search(r"[?&]page=\d+", a["href"])), None)
            if next_link is None:
                break
            index_url = urljoin(BASE, next_link)
    save()
    print(json.dumps({"records": len(records), "expiry_updates": len(expiry_updates),
                      "failures": len(failures), "new": new_count, "output": str(args.output)}))


if __name__ == "__main__":
    main()
