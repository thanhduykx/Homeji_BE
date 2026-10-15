"""Render reviewed source refresh + expired-source cleanup for a backed-up transaction."""
import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

from collect_rental_sources import sql_string
from check_rental_source_expiry import source_identity
from prepare_rental_source_batch import validate_record


def render_refresh_sql(evidence, baseline):
    if evidence.get("availability_verified") is not False:
        raise ValueError("Source checks cannot verify physical availability")
    if not baseline or len({item["id"] for item in baseline}) != len(baseline):
        raise ValueError("Expected a nonempty unique database backup")
    now = datetime.now(timezone.utc)
    lines = ["SET LOCAL standard_conforming_strings = on;",
             "SET LOCAL lock_timeout = '5s';", "SET LOCAL statement_timeout = '60s';",
             "LOCK TABLE homeji.rental_source_listings IN SHARE ROW EXCLUSIVE MODE;",
             "CREATE TEMP TABLE rental_refresh_baseline ON COMMIT DROP AS SELECT value AS row FROM jsonb_array_elements("
             + sql_string(json.dumps(baseline, ensure_ascii=False)) + "::jsonb);",
             """DO $guard$ BEGIN
               IF EXISTS (SELECT 1 FROM rental_refresh_baseline b
                 LEFT JOIN homeji.rental_source_listings s ON s.id=(b.row->>'id')::uuid
                 WHERE s.id IS NULL OR to_jsonb(s) IS DISTINCT FROM b.row) THEN
                 RAISE EXCEPTION 'Source catalog changed since backup; inspect and recollect before applying';
               END IF;
             END $guard$;"""]
    known_urls = {item["source_url"] for item in baseline}
    for item in evidence["expiry_updates"]:
        if not source_identity(item["source_url"]):
            raise ValueError("Invalid source identity")
        if item["source_url"] not in known_urls:
            continue  # Newly collected records are inserted below with their expiry.
        checked = datetime.fromisoformat(item["source_checked_at"])
        if checked.tzinfo is None or checked > now:
            raise ValueError("Invalid source check timestamp")
        expiry = item["source_expires_at"]
        lines.append("UPDATE homeji.rental_source_listings SET source_expires_at=COALESCE("
                     + sql_string(expiry) + "::timestamptz,source_expires_at),source_checked_at="
                     + sql_string(item["source_checked_at"]) + "::timestamptz WHERE source_url="
                     + sql_string(item["source_url"]) + " AND (source_checked_at IS NULL OR source_checked_at <= "
                     + sql_string(item["source_checked_at"]) + "::timestamptz);")
    seen = set()
    for record in evidence["records"]:
        validate_record(record)
        identity = (record["source"], record["source_id"])
        if identity in seen:
            raise ValueError("Duplicate refreshed identity")
        seen.add(identity)
        expiry = record.get("source_expires_at")
        if not expiry or datetime.fromisoformat(expiry) <= now:
            continue
        values = [sql_string(record[key]) for key in
                  ("id", "source", "source_id", "source_url", "title", "address", "district")]
        values += [str(record["price"]), str(record["area"]),
                   sql_string(json.dumps(record["image_urls"], ensure_ascii=False)) + "::jsonb"]
        values += [sql_string(record[key]) + "::timestamptz" for key in
                   ("source_updated_at", "collected_at", "source_expires_at", "source_checked_at")]
        lines.append("DO $expiry$ BEGIN IF CURRENT_TIMESTAMP >= " + sql_string(expiry)
                     + "::timestamptz THEN RAISE EXCEPTION 'Refreshed advertisement expired before apply'; END IF; END $expiry$;")
        lines.append("INSERT INTO homeji.rental_source_listings "
                     "(id,source,source_id,source_url,title,address,district,price,area,image_urls,source_updated_at,collected_at,source_expires_at,source_checked_at) VALUES ("
                     + ",".join(values) + ") ON CONFLICT (source,source_id) DO UPDATE SET "
                     "source_url=EXCLUDED.source_url,title=EXCLUDED.title,address=EXCLUDED.address,district=EXCLUDED.district,"
                     "price=EXCLUDED.price,area=EXCLUDED.area,image_urls=EXCLUDED.image_urls,source_updated_at=EXCLUDED.source_updated_at,"
                     "collected_at=EXCLUDED.collected_at,source_expires_at=EXCLUDED.source_expires_at,source_checked_at=EXCLUDED.source_checked_at "
                     "WHERE rental_source_listings.source_checked_at IS NULL OR rental_source_listings.source_checked_at <= EXCLUDED.source_checked_at;")
    lines.append("""DELETE FROM homeji.rental_source_listings s USING rental_refresh_baseline b
                 WHERE s.id=(b.row->>'id')::uuid AND s.source_expires_at <= CURRENT_TIMESTAMP;""")
    lines.append("""DO $verify$ BEGIN
                  IF EXISTS (SELECT 1 FROM homeji.rental_source_listings WHERE source_expires_at <= CURRENT_TIMESTAMP) THEN
                    RAISE EXCEPTION 'Expired source advertisements remain';
                  END IF;
                 END $verify$;""")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    evidence = json.loads(args.evidence.read_text(encoding="utf-8-sig"))
    baseline = json.loads(args.baseline.read_text(encoding="utf-8-sig"))
    args.output.write_text(render_refresh_sql(evidence, baseline), encoding="utf-8")
    print(json.dumps({"sql": str(args.output), "source_checks": len(evidence["expiry_updates"]), "fact_refreshes": len(evidence["records"])}))


if __name__ == "__main__":
    main()
