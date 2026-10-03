import unittest
from render_source_batch_sql import render_insert_sql
import test_prepare_rental_source_batch as fixtures


class ImportSqlTests(unittest.TestCase):
    def test_preserves_existing_rows_and_quotes_source_text(self):
        record = fixtures.BatchTests().record(title="Nhà O'Brien")
        record.update(source_updated_at=None, collected_at="2026-10-03T00:00:00Z")
        sql = render_insert_sql({"records": [record]})
        self.assertIn("'Nhà O''Brien'", sql)
        self.assertIn("ON CONFLICT (source,source_id) DO NOTHING", sql)
        self.assertNotIn("DO UPDATE", sql)
        self.assertTrue(sql.startswith("BEGIN;"))
        self.assertTrue(sql.endswith("COMMIT;\n"))

    def test_rejects_expired_before_producing_sql(self):
        with self.assertRaises(ValueError):
            render_insert_sql({"records": [fixtures.BatchTests().record(source_status="expired")]})

    def test_rejects_duplicates(self):
        record = fixtures.BatchTests().record()
        with self.assertRaises(ValueError):
            render_insert_sql({"records": [record, record]})
