import unittest
from prepare_rental_source_batch import combine, validate_record


class BatchTests(unittest.TestCase):
    def record(self, **changes):
        record = {"id": "e3a4a777-e34a-4949-8891-992e196f9111", "source": "muaban",
                  "source_id": "123", "source_url": "https://muaban.net/room-id123", "district": "thu-duc",
                  "title": "Phòng gần trường", "address": "Thủ Đức, Hồ Chí Minh", "price": 2000000,
                  "area": 20, "image_urls": ["https://cloud.muaban.net/room.jpg"]}
        record.update(changes)
        return record

    def test_deduplicates_source_identity_without_asserting_availability(self):
        result = combine([{"records": [self.record(), self.record()]}])
        self.assertEqual(1, len(result["records"]))
        self.assertFalse(result["records"][0]["availability_verified"])

    def test_keeps_expired_out_of_import_batch(self):
        result = combine([{"records": [self.record(source_status="expired")]}])
        self.assertFalse(result["records"])
        self.assertEqual("expired", result["rejected"][0]["reason"])

    def test_rejects_nan_and_untrusted_host(self):
        for changes in ({"price": "NaN"}, {"source_url": "https://localhost/room"}):
            with self.assertRaises(ValueError):
                validate_record(self.record(**changes))
