import json
import unittest
from collect_muaban_sources import extract_muaban_listing


class MuabanParserTests(unittest.TestCase):
    def fixture(self, **changes):
        listing = {"id": 123, "city_id": 30, "is_expired": False,
                   "service_end": "2100-01-01T00:00:00Z", "publish_at": "2026-10-03T00:00:00Z",
                   "title": "Phòng trọ gần trường", "address": "Linh Trung, Thủ Đức, Hồ Chí Minh",
                   "price": 2000000, "attributes": [{"value": "16 m²"}],
                   "images": [{"url": "https://cloud.muaban.net/room.jpg"}],
                   "body": "Untrusted source description", "phone_display": "Private contact"}
        listing.update(changes)
        return '<script id="__NEXT_DATA__" type="application/json">' + json.dumps({
            "props": {"pageProps": {"classified": listing,
                                    "similars": {"items": [{"price": 1, "id": 999}]}}}}) + '</script>'

    def parse(self, html):
        return extract_muaban_listing(html,
            "https://muaban.net/bat-dong-san/cho-thue-nha-tro-phong-tro-quan-thu-duc-ho-chi-minh/phong-id123",
            "thu-duc", "2026-10-03T00:00:00Z")

    def test_own_facts_and_no_contact_description_or_verification(self):
        record = self.parse(self.fixture())
        self.assertEqual(2000000, record["price"])
        self.assertEqual(16, record["area"])
        self.assertEqual("123", record["source_id"])
        self.assertFalse(record["availability_verified"])
        self.assertNotIn("body", record)
        self.assertNotIn("phone_display", record)

    def test_rejects_expired(self):
        with self.assertRaises(ValueError):
            self.parse(self.fixture(is_expired=True))

    def test_rejects_past_expiration_even_if_flag_is_false(self):
        with self.assertRaises(ValueError):
            self.parse(self.fixture(service_end="2020-01-01T00:00:00Z"))

    def test_rejects_identity_mismatch(self):
        with self.assertRaises(ValueError):
            self.parse(self.fixture(id=999))

    def test_rejects_out_of_scope_address(self):
        with self.assertRaises(ValueError):
            self.parse(self.fixture(address="Dĩ An, Bình Dương"))

    def test_rejects_unknown_gallery_host(self):
        with self.assertRaises(ValueError):
            self.parse(self.fixture(images=[{"url": "https://localhost/room.jpg"}]))
