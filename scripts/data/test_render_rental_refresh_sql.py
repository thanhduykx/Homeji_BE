import unittest
from datetime import datetime, timedelta, timezone

from render_rental_refresh_sql import render_refresh_sql
import test_prepare_rental_source_batch as fixtures


class RentalRefreshSqlTests(unittest.TestCase):
    def evidence(self):
        now = datetime.now(timezone.utc)
        record = fixtures.BatchTests().record(title="Nhà O'Brien")
        record.update(source_updated_at=None, collected_at=now.isoformat(),
                      source_checked_at=now.isoformat(), source_expires_at=(now + timedelta(days=2)).isoformat())
        baseline = [{"id": record["id"], "source_url": record["source_url"]}]
        evidence = {"availability_verified": False, "records": [record], "expiry_updates": [
            {"source_url": record["source_url"], "source_checked_at": now.isoformat(), "source_expires_at": None}]}
        return evidence, baseline

    def test_backup_guard_identity_preservation_and_expiry_cleanup_are_atomic(self):
        evidence, baseline = self.evidence()
        sql = render_refresh_sql(evidence, baseline)
        self.assertIn("Source catalog changed since backup", sql)
        self.assertIn("'Nhà O''Brien'", sql)
        self.assertIn("ON CONFLICT (source,source_id) DO UPDATE", sql)
        self.assertNotIn("id=EXCLUDED.id", sql)
        self.assertIn("COALESCE(NULL::timestamptz,source_expires_at)", sql)
        self.assertIn("s.id=(b.row->>'id')::uuid AND s.source_expires_at <= CURRENT_TIMESTAMP", sql)
        self.assertNotIn("COMMIT;", sql)  # The runner owns the backup and transaction.

    def test_new_source_expiry_is_inserted_without_updating_unrelated_rows(self):
        evidence, baseline = self.evidence()
        baseline[0]["source_url"] = "https://phongtro123.com/older-pr456.html"
        sql = render_refresh_sql(evidence, baseline)
        self.assertNotIn("UPDATE homeji.rental_source_listings SET source_expires_at", sql)
        self.assertIn("Refreshed advertisement expired before apply", sql)

    def test_unverified_availability_empty_backup_and_duplicate_records_are_rejected(self):
        evidence, baseline = self.evidence()
        with self.assertRaises(ValueError):
            render_refresh_sql(evidence, [])
        with self.assertRaises(ValueError):
            render_refresh_sql({**evidence, "availability_verified": True}, baseline)
        with self.assertRaises(ValueError):
            render_refresh_sql({**evidence, "records": evidence["records"] * 2}, baseline)

    def test_expired_record_is_never_inserted(self):
        evidence, baseline = self.evidence()
        evidence["records"][0]["source_expires_at"] = "2020-01-01T00:00:00Z"
        self.assertNotIn("INSERT INTO", render_refresh_sql(evidence, baseline))


if __name__ == "__main__":
    unittest.main()
