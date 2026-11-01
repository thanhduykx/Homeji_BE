import json
from datetime import datetime, timezone
import unittest
from collect_quan9_sources import extract_mogi, extract_phongtrovn


class Quan9CollectorTests(unittest.TestCase):
    def test_mogi_uses_own_offer_and_gallery_not_agent_or_related_ad(self):
        metadata = {'@type': 'Person', 'telephone': 'not-collected', 'makesOffer': {
            'priceSpecification': {'priceCurrency': 'VND', 'price': 3000000},
            'itemOffered': {'name': 'Room', 'address': 'Phường Phước Bình, Quận 9, TPHCM', 'floorSize': '25 m²'}}}
        html = '<script type="application/ld+json">' + json.dumps(metadata, ensure_ascii=False) + '</script><div class="media-item"><img data-src="https://cloud.mogi.vn/room.jpg"></div><img src="https://cloud.mogi.vn/related.jpg">'
        record = extract_mogi(html, 'https://mogi.vn/quan-9/thue-phong-tro-khu-nha-tro/room-id123', datetime.now(timezone.utc).isoformat())
        self.assertEqual(3000000, record['price'])
        self.assertEqual(['https://cloud.mogi.vn/room.jpg'], record['image_urls'])
        self.assertNotIn('telephone', record)
        with self.assertRaises(ValueError):
            extract_mogi(html.replace('Quận 9', 'Quận 2'), record['source_url'], record['collected_at'])

    def test_third_source_requires_own_identity_area_currency_and_expiry(self):
        url = 'https://phongtro.vn/cho-thue-phong-tro-long-phuoc/123'
        listing = {'@type': 'RealEstateListing', 'name': 'Room', 'url': url, 'image': ['https://file.phongtro.vn/own.jpg'],
                   'offers': {'price': 3500000, 'priceCurrency': 'VND'}, 'about': {
                       'floorSize': {'value': 40, 'unitCode': 'MTK'},
                       'address': {'streetAddress': '74 Đường số 8', 'addressLocality': 'Long Phước', 'addressRegion': 'Hồ Chí Minh'}}}
        html = '<script type="application/ld+json">' + json.dumps(listing) + '</script><li><p>Ngày hết hạn</p><time datetime="2026-11-06T23:51:16Z">06/11/2026</time></li>'
        record = extract_phongtrovn(html, url, datetime.now(timezone.utc).isoformat())
        self.assertEqual('2026-11-06T23:51:16Z', record['source_expires_at'])
        self.assertEqual(40, record['area'])
        with self.assertRaises(ValueError): extract_phongtrovn(html.replace('MTK', 'unknown'), url, record['collected_at'])
        with self.assertRaises(ValueError): extract_phongtrovn(html.replace('Ngày hết hạn', 'Ngày khác'), url, record['collected_at'])


if __name__ == '__main__': unittest.main()
