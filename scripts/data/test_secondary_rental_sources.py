import unittest
from collect_secondary_rental_sources import extract_secondary_listing


class SecondarySourceTests(unittest.TestCase):
    def html(self, address="Phường Hiệp Bình Chánh, Thủ Đức, Hồ Chí Minh", expired=True):
        return f'''<div id="overview"><h1>Phòng gần trường</h1>
        <a href="cho-thue-phong-tro-thu-duc">Khu vực</a>
        <p class="post-address"><span>Địa chỉ: <span>{address}</span></span></p>
        <span class="post-price">3,5 Triệu/tháng</span><span class="post-acreage">24 m²</span>
        <span class="post-published"><span>16/09/2026</span></span>
        <div class="post-images"><img src="https://img.thuephongtro.com/real.jpg"></div>
        {'<div id="da_het_han"></div>' if expired else ''}</div>
        <img src="https://img.thuephongtro.com/unrelated.jpg">'''

    def parse(self, html):
        return extract_secondary_listing(html, "https://thuephongtro.com/phong-123.html", "thu-duc", "2026-10-03T00:00:00Z")

    def test_expired_is_not_available_and_gallery_excludes_related_ads(self):
        record = self.parse(self.html())
        self.assertEqual("expired", record["source_status"])
        self.assertFalse(record["availability_verified"])
        self.assertEqual(3500000, record["price"])
        self.assertEqual(["https://img.thuephongtro.com/real.jpg"], record["image_urls"])

    def test_unexpired_still_is_not_verified(self):
        self.assertEqual("unconfirmed", self.parse(self.html(expired=False))["source_status"])

    def test_rejects_nearby_but_outside_scope(self):
        with self.assertRaises(ValueError):
            self.parse(self.html("Dĩ An, Bình Dương"))

    def test_rejects_wrong_source_district(self):
        with self.assertRaises(ValueError):
            self.parse(self.html().replace("cho-thue-phong-tro-thu-duc", "cho-thue-phong-tro-quan-2"))

    def test_rejects_untrusted_image_host(self):
        with self.assertRaises(ValueError):
            self.parse(self.html().replace("img.thuephongtro.com/real.jpg", "localhost/real.jpg"))
