"""Check original gallery URLs against image signatures without copying images or editing data."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import threading
import time
from urllib.parse import urlparse

import requests
from collect_rental_sources import AGENT, fetch


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    records = json.loads(args.snapshot.read_text(encoding="utf-8-sig"))
    urls = list(dict.fromkeys(url for item in records for url in item["image_urls"]))
    local = threading.local()
    stopped = threading.Event()

    def check(url):
        if stopped.is_set():
            return {"url": url, "status": "unattempted_after_rate_limit"}
        host = urlparse(url).hostname
        if host not in ("pt123.cdn.static123.com", "cloud.muaban.net"):
            return {"url": url, "status": "disallowed_origin"}
        if not hasattr(local, "session"):
            local.session = requests.Session()
            local.session.headers["User-Agent"] = AGENT
        try:
            time.sleep(0.2)
            fetch(local.session, url, host, image=True)
            return {"url": url, "status": "image_signature_valid"}
        except requests.HTTPError as error:
            if error.response.status_code == 429:
                stopped.set()
            return {"url": url, "status": "http_error", "http_status": error.response.status_code}
        except (ValueError, requests.RequestException) as error:
            return {"url": url, "status": type(error).__name__}

    with ThreadPoolExecutor(max_workers=3) as pool:
        results = list(pool.map(check, urls))
    valid = sum(item["status"] == "image_signature_valid" for item in results)
    output = {"unique_urls": len(urls), "valid": valid, "other": len(urls)-valid, "results": results}
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in output.items() if key != "results"}))


if __name__ == "__main__":
    main()
