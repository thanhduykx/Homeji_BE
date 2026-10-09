"""Generate a transactional, insert-only import. Existing source facts stay intact."""
import argparse
import json
from pathlib import Path
from collect_rental_sources import sql_string
from prepare_rental_source_batch import combine


def render_insert_sql(batch):
    checked = combine([batch])
    if checked["rejected"] or len(checked["records"]) != len(batch["records"]):
        raise ValueError("Batch contains expired or duplicate source records")
    statements = ["BEGIN;", "SET LOCAL standard_conforming_strings = on;",
                  "SET LOCAL lock_timeout = '5s';", "SET LOCAL statement_timeout = '30s';"]
    for record in checked["records"]:
        values = [sql_string(record[key]) for key in
                  ("id", "source", "source_id", "source_url", "title", "address", "district")]
        values += [str(record["price"]), str(record["area"]),
                   sql_string(json.dumps(record["image_urls"], ensure_ascii=False)) + "::jsonb",
                   sql_string(record["source_updated_at"]), sql_string(record["collected_at"]),
                   sql_string(record.get("source_expires_at")), sql_string(record.get("source_checked_at"))]
        statements.append("INSERT INTO homeji.rental_source_listings "
                          "(id,source,source_id,source_url,title,address,district,price,area,image_urls,source_updated_at,collected_at,source_expires_at,source_checked_at) "
                          "VALUES (" + ",".join(values) + ") ON CONFLICT (source,source_id) DO NOTHING;")
    statements.append("COMMIT;")
    return "\n".join(statements) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("batch", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    batch = json.loads(args.batch.read_text(encoding="utf-8"))
    sql = render_insert_sql(batch)
    args.output.write_text(sql, encoding="utf-8")
    print(json.dumps({"sql": str(args.output), "records": len(batch["records"]), "mode": "insert_only"}))


if __name__ == "__main__":
    main()
