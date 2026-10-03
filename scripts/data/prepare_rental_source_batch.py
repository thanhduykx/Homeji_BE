"""Validate and combine collected facts; never mutates database or publishes ads."""
import argparse
import hashlib
import json
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path
from urllib.parse import urlparse
from uuid import UUID

SOURCES = {"phongtro123": ("phongtro123.com", "pt123.cdn.static123.com"),
           "muaban": ("muaban.net", "cloud.muaban.net"),
           "thuephongtro": ("thuephongtro.com", "img.thuephongtro.com")}


def valid_url(value, host):
    parsed = urlparse(value)
    return (parsed.scheme == "https" and parsed.hostname == host and not parsed.username
            and not parsed.password and parsed.port in (None, 443) and len(value) <= 1000)


def validate_record(record):
    UUID(record["id"])
    source_host, image_host = SOURCES[record["source"]]
    if not record["source_id"].isascii() or not record["source_id"].isdigit() or len(record["source_id"]) > 80:
        raise ValueError("Invalid source identity")
    if not valid_url(record["source_url"], source_host) or record["district"] not in ("quan-9", "thu-duc"):
        raise ValueError("Source or district outside allowlist")
    if not 1 <= len(record["title"].strip()) <= 200 or not 1 <= len(record["address"].strip()) <= 500:
        raise ValueError("Missing or oversized title/address")
    price, area = Decimal(str(record["price"])), Decimal(str(record["area"]))
    if not price.is_finite() or not area.is_finite() or not 0 < price <= 100000000 or not 0 < area <= 1000:
        raise ValueError("Invalid numeric facts")
    if not 1 <= len(record["image_urls"]) <= 10 or any(not valid_url(url, image_host) for url in record["image_urls"]):
        raise ValueError("Invalid gallery")
    if record.get("availability_verified", False):
        raise ValueError("Collectors cannot verify physical room availability")
    return record


def combine(snapshots):
    records, rejected, seen = [], [], set()
    for snapshot in snapshots:
        for record in snapshot["records"]:
            validate_record(record)
            key = (record["source"], record["source_id"])
            if key in seen:
                continue
            seen.add(key)
            expires = record.get("source_expires_at")
            if record.get("source_status") == "expired" or (expires and datetime.fromisoformat(expires) <= datetime.now(timezone.utc)):
                rejected.append({"source": key[0], "source_id": key[1], "reason": "expired"})
                continue
            records.append({**record, "availability_verified": False,
                            "rights_status": "not_confirmed", "source_status": "unconfirmed"})
    return {"records": records, "rejected": rejected,
            "image_storage": "original URLs only; no image binary copied",
            "publication_status": "not_published",
            "availability_notice": "Source advertisements, not verified available rooms"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshots", type=Path, nargs="+")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    payload = combine([json.loads(path.read_text(encoding="utf-8")) for path in args.snapshots])
    content = json.dumps(payload, ensure_ascii=False, indent=2)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(content, encoding="utf-8")
    print(json.dumps({"batch": str(args.output), "records": len(payload["records"]),
                      "rejected": len(payload["rejected"]), "images": sum(len(x["image_urls"]) for x in payload["records"]),
                      "sha256": hashlib.sha256(content.encode()).hexdigest()}))


if __name__ == "__main__":
    main()
