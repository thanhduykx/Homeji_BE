import unittest
from collect_rental_sources import extract_listing, extract_source_expiry, render_sql


class SourceParserTests(unittest.TestCase):
    def test_expiry_uses_own_label_and_vietnam_timezone(self):
        html = '<table><tr><td>Ngày hết hạn:</td><td>Thứ 7, 10:55 12/09/2026</td></tr></table><article>20:00 01/01/2030</article>'
        self.assertEqual("2026-09-12T03:55:00+00:00", extract_source_expiry(html))
        self.assertIsNone(extract_source_expiry('<p>Cập nhật 10:55 12/09/2026</p>'))
        self.assertIsNone(extract_source_expiry(html.replace('12/09/2026', '31/02/2026')))

    def html(self, district="quan-9"):
        return '''<script type="application/ld+json">{"@type":"WebPage","breadcrumb":{
        "itemListElement":[{"item":"/tinh-thanh/ho-chi-minh/''' + district + '''"}]}}</script>
        <script type="application/ld+json">{"@type":"Hostel","name":"Phòng 30m2",
        "address":{"streetAddress":"Phường Phước Long, Hồ Chí Minh"},"priceRange":"2000000"}</script>
        <div><span class="text-green fs-5 fw-bold">2 triệu/tháng</span><span>20 m<sup>2</sup></span></div>
        <div id="carousel_Photos"><div class="carousel-inner"><img src="https://pt123.cdn.static123.com/real.jpg"></div></div>
        <img src="https://pt123.cdn.static123.com/unrelated-ad.jpg">'''

    def test_uses_explicit_area_and_own_gallery_instead_of_title_or_related_ad(self):
        listing = extract_listing(self.html(), "https://phongtro123.com/tin-pr123.html", "quan-9", "2026-10-03T00:00:00Z")
        self.assertEqual(20, listing["area"])
        self.assertEqual(["https://pt123.cdn.static123.com/real.jpg"], listing["image_urls"])

    def test_rejects_wrong_district_even_if_collected_from_allowed_index(self):
        with self.assertRaises(ValueError):
            extract_listing(self.html("quan-1"), "https://phongtro123.com/tin-pr123.html", "quan-9", "2026-10-03T00:00:00Z")

    def test_sql_quotes_untrusted_listing_title(self):
        listing = extract_listing(self.html(), "https://phongtro123.com/tin-pr123.html", "quan-9", "2026-10-03T00:00:00Z")
        listing["title"] = "Nhà O'Brien"
        sql = render_sql([listing])
        self.assertIn("'Nhà O''Brien'", sql)
        self.assertIn("ON CONFLICT (source,source_id)", sql)


if __name__ == "__main__":
    unittest.main()
