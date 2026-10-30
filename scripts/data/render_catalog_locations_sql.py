"""Render an insert-only catalog batch. Run using RentalCatalogRepair's backup transaction."""
import argparse
import json
import math
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse


def quote(value):
    return "NULL" if value is None else "'" + str(value).replace("'", "''") + "'"


def coordinates(item, latitude="latitude", longitude="longitude"):
    lat, lng = item.get(latitude), item.get(longitude)
    if lat is None and lng is None:
        return "NULL", "NULL"
    if (isinstance(lat, bool) or isinstance(lng, bool) or not isinstance(lat, (int, float))
            or not isinstance(lng, (int, float)) or not math.isfinite(lat) or not math.isfinite(lng)
            or not 10.7 <= lat <= 10.93 or not 106.72 <= lng <= 106.9):
        raise ValueError("Coordinates must be a reviewed point in the Homeji service area")
    return str(lat), str(lng)


def render(batch):
    if not 1 <= len(batch["rentals"]) <= 30 or not 1 <= len(batch["food"]) <= 20:
        raise ValueError("Unexpected batch size")
    sql = ["-- RentalCatalogRepair supplies the transaction and backs up all affected tables.",
           "SET LOCAL standard_conforming_strings = on;", "SET LOCAL lock_timeout = '5s';",
           "SET LOCAL statement_timeout = '30s';"]
    seen = set()
    for row in batch["rentals"]:
        key = row["source"], row["source_id"]
        if key in seen or row["source"] != "phongtro123" or not re.fullmatch(r"\d+", row["source_id"]):
            raise ValueError("Duplicate or unsupported source")
        seen.add(key)
        if urlparse(row["source_url"]).hostname != "phongtro123.com" or not row["source_url"].startswith("https://"):
            raise ValueError("Wrong rental source")
        expiry = datetime.fromisoformat(row["source_expires_at"])
        if expiry <= datetime.now(timezone.utc):
            raise ValueError("Expired advertisement")
        if not 0 < row["price"] <= 100000000 or not 0 < row["area"] <= 1000:
            raise ValueError("Invalid price or area")
        if not 1 <= len(row["image_urls"]) <= 10 or any(not image.startswith("https://pt123.cdn.static123.com/") for image in row["image_urls"]):
            raise ValueError("Images must belong to the source's own gallery")
        lat, lng = coordinates(row)
        sql.append(f"DO $expiry$ BEGIN IF CURRENT_TIMESTAMP >= {quote(row['source_expires_at'])}::timestamptz THEN RAISE EXCEPTION 'Source expired before import'; END IF; END $expiry$;")
        values = [quote(row[k]) for k in ("id", "source", "source_id", "source_url", "title", "address", "district")]
        values += [str(row["price"]), str(row["area"]), quote(json.dumps(row["image_urls"], ensure_ascii=False)) + "::jsonb",
                   quote(row["source_updated_at"]) + "::timestamptz", "CURRENT_TIMESTAMP",
                   quote(row["source_expires_at"]) + "::timestamptz", "CURRENT_TIMESTAMP", lat, lng]
        sql.append("INSERT INTO homeji.rental_source_listings (id,source,source_id,source_url,title,address,district,price,area,image_urls,source_updated_at,collected_at,source_expires_at,source_checked_at,latitude,longitude) VALUES (" + ",".join(values) + ") ON CONFLICT (source,source_id) DO NOTHING;")
    for row in batch["food"]:
        uri = urlparse(row["sourceUrl"])
        if uri.scheme != "https" or uri.hostname not in ("www.tiemcomvien.com", "www.google.com") or uri.username or uri.password:
            raise ValueError("Unapproved venue source")
        if uri.hostname == "www.google.com" and not uri.path.startswith("/maps/"):
            raise ValueError("Google source must identify a map location")
        if any(len(row[key]) > maximum for key, maximum in (("name", 200), ("address", 500), ("sourceUrl", 1000), ("mapsSearchUrl", 1000), ("verificationNote", 1000))):
            raise ValueError("Venue field exceeds database limit")
        lat, lng = coordinates(row)
        values = [quote(row[key]) for key in ("id", "name", "address", "sourceUrl", "mapsSearchUrl", "verificationNote")]
        values += ["CURRENT_TIMESTAMP", lat, lng]
        sql.append("INSERT INTO homeji.food_source_places (id,name,address,source_url,maps_search_url,verification_note,collected_at,latitude,longitude) VALUES (" + ",".join(values) + ") ON CONFLICT (name,address) DO NOTHING;")
    return "\n".join(sql) + "\n"


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("batch", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    batch = json.loads(args.batch.read_text(encoding="utf-8"))
    args.output.write_text(render(batch), encoding="utf-8")
    print(json.dumps({"rentals": len(batch["rentals"]), "food": len(batch["food"]), "mode": "insert_only", "sql": str(args.output)}))
