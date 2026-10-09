"""Check source expiry dates without changing database or importing ad text/images.

Input: {"sourceLinks": [public Phongtro123 or Muaban advertisement URLs]}.
Output is a reviewable metadata-only JSON; a separate step applies it.
"""
import argparse
import json
import re
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse
from urllib.robotparser import RobotFileParser

import requests
from collect_rental_sources import AGENT, extract_source_expiry, fetch
from collect_muaban_sources import page_data


def source_identity(url):
    try:
        parsed = urlparse(url)
        match = re.search(r"-pr([0-9]+)\.html$" if parsed.hostname == "phongtro123.com" else r"-id([0-9]+)$", parsed.path)
        if (not match or parsed.scheme != "https" or parsed.hostname not in ("phongtro123.com", "muaban.net")
                or parsed.port not in (None, 443) or parsed.username or parsed.password or parsed.query or parsed.fragment
                or len(url) > 1000):
            return None
        return match[1]
    except (TypeError, ValueError):
        return None


def checked_expiry(html, url):
    if urlparse(url).hostname == "phongtro123.com":
        return extract_source_expiry(html)
    listing = page_data(html)["classified"]
    if str(listing["id"]) != source_identity(url):
        raise ValueError("Page identity differs from requested advertisement")
    expiry = datetime.fromisoformat(listing["service_end"])
    return expiry.astimezone(timezone.utc).isoformat() if expiry.tzinfo is not None else None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    urls = json.loads(args.input.read_text(encoding="utf-8"))["sourceLinks"]
    if not isinstance(urls, list) or not 1 <= len(urls) <= 100 or any(not isinstance(url, str) for url in urls) or len(set(urls)) != len(urls):
        parser.error("Provide 1–100 unique source URLs")
    records, failures = [], []
    with requests.Session() as session:
        session.headers["User-Agent"] = AGENT
        robots_by_host = {}
        for url in urls:
            identity = source_identity(url)
            if not identity:
                failures.append({"source_url": url, "reason": "disallowed"})
                continue
            try:
                host = urlparse(url).hostname
                if host not in robots_by_host:
                    robots = RobotFileParser()
                    robots.parse(fetch(session, f"https://{host}/robots.txt", host).splitlines())
                    robots_by_host[host] = robots
                if not robots_by_host[host].can_fetch(AGENT, url):
                    failures.append({"source_url": url, "reason": "robots_disallowed"})
                    continue
                time.sleep(1)
                expiry = checked_expiry(fetch(session, url, host), url)
                records.append({"source": "phongtro123" if host == "phongtro123.com" else "muaban", "source_id": identity, "source_url": url,
                                "source_expires_at": expiry,
                                "source_checked_at": datetime.now(timezone.utc).isoformat()})
                print(f"Checked source {identity}", flush=True)
            except (ValueError, KeyError, TypeError, requests.RequestException) as error:
                failures.append({"source_url": url, "reason": type(error).__name__})
                if isinstance(error, requests.HTTPError) and error.response is not None and error.response.status_code == 429:
                    break
    unattempted = len(urls) - len(records) - len(failures)
    args.output.write_text(json.dumps({"records": records, "failures": failures, "unattempted": unattempted}, indent=2), encoding="utf-8")
    print(json.dumps({"checked": len(records), "failed": len(failures), "unattempted": unattempted, "output": str(args.output)}))


if __name__ == "__main__":
    main()
