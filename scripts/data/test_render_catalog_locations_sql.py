import copy
import json
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from render_catalog_locations_sql import render


class CatalogImportTests(unittest.TestCase):
    def setUp(self):
        self.batch = json.loads(Path(__file__).with_name("catalog-20261030.json").read_text(encoding="utf-8"))
        for row in self.batch["rentals"]:
            row["source_expires_at"] = (datetime.now(timezone.utc) + timedelta(days=2)).isoformat()

    def test_insert_only_and_quotes_source_text_without_attributing_to_homeji_sellers(self):
        self.batch["food"][0]["name"] = "Quán O'Brien"
        sql = render(self.batch)
        self.assertIn("O''Brien", sql)
        self.assertEqual(sql.count("INSERT INTO homeji.rental_source_listings"), 12)
        self.assertEqual(sql.count("INSERT INTO homeji.food_source_places"), 6)
        self.assertNotIn("INSERT INTO homeji.rental_posts", sql)
        self.assertNotIn("INSERT INTO homeji.marketplace_posts", sql)
        self.assertNotIn("DO UPDATE", sql)
        self.assertNotIn("DELETE FROM", sql)

    def test_expired_or_duplicate_rental_prevents_the_entire_batch(self):
        self.batch["rentals"][0]["source_expires_at"] = "2020-01-01T00:00:00+00:00"
        with self.assertRaises(ValueError):
            render(self.batch)
        self.setUp()
        self.batch["rentals"].append(copy.deepcopy(self.batch["rentals"][0]))
        with self.assertRaises(ValueError):
            render(self.batch)

    def test_location_and_source_validation_cannot_be_bypassed(self):
        for change in [{"latitude": 0}, {"longitude": None}, {"latitude": "10.8); DROP TABLE x;--"}, {"sourceUrl": "javascript:alert(1)"}]:
            batch = copy.deepcopy(self.batch)
            batch["food"][0].update(change)
            with self.assertRaises(ValueError):
                render(batch)


if __name__ == "__main__":
    unittest.main()
