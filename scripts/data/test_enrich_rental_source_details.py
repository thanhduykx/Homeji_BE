import json
import unittest
from enrich_rental_source_details import extract_details,publisher

class SourceDetailTests(unittest.TestCase):
    def test_contact_belongs_to_the_listing_not_platform_or_related_ads(self):
        url='https://mogi.vn/room-id123'
        items=[{'@type':'RealEstateAgent','telephone':'02873001234'},
            {'@type':'Person','name':'Nguyễn An','telephone':'+84 901 234 567','makesOffer':{'itemOffered':{'description':'Có bếp<br />Điện 3.500đ/kWh'}}}]
        html=''.join('<script type="application/ld+json">'+json.dumps(item)+'</script>' for item in items)
        actual=extract_details(html,'mogi',url)
        self.assertEqual('0901234567',actual['contact_phone']);self.assertEqual('Có bếp\nĐiện 3.500đ/kWh',actual['description'])
        other='<script type="application/ld+json">'+json.dumps({'@type':'Hostel','url':'https://phongtro123.com/other.html','telephone':'0901111111'})+'</script>'
        self.assertIsNone(extract_details(other,'phongtro123','https://phongtro123.com/own.html')['contact_phone'])
    def test_description_uses_own_section_without_importing_footer_hotline(self):
        html='<h3>Thông tin mô tả</h3><div>Phòng có cửa sổ.</div><footer><a href="tel:0901111111">Tổng đài</a></footer>'
        actual=extract_details('<section>'+html.split('<footer>')[0]+'</section><footer>Tổng đài</footer>','phongtrovn','https://phongtro.vn/own/123')
        self.assertEqual('Phòng có cửa sổ.',actual['description']);self.assertIsNone(actual['contact_phone'])
    def test_listing_embed_and_person_are_used_not_the_platform_office(self):
        office={'@type':'RealEstateAgent','name':'Mogi','geo':{'latitude':10.7339,'longitude':106.7092}}
        person={'@type':'Person','name':'Nguyễn An','makesOffer':{'itemOffered':{'name':'Room'}}}
        html=''.join('<script type="application/ld+json">'+json.dumps(item)+'</script>' for item in [office,person])
        missing=extract_details(html,'mogi','https://mogi.vn/room-id123')
        self.assertEqual('Nguyễn An',missing['publisher_name']);self.assertIsNone(missing['latitude'])
        html+='<script type="text/template"><iframe src="https://www.google.com/maps/embed/v1/place?key=not-collected&amp;q=10.82986641,106.78774261"></iframe></script>'
        actual=extract_details(html,'mogi','https://mogi.vn/room-id123')
        self.assertEqual(10.82986641,actual['latitude']);self.assertNotIn('key',actual)
    def test_names_and_source_identity_are_not_invented(self):
        self.assertIsNone(publisher('0901234567'));self.assertIsNone(publisher('https://example.invalid'))
        webpage={'@type':'WebPage','url':'https://phongtro123.com/own-pr123.html','author':{'name':'Trần Bình'}}
        html='<script type="application/ld+json">'+json.dumps(webpage)+'</script>'
        self.assertEqual('Trần Bình',extract_details(html,'phongtro123',webpage['url'])['publisher_name'])
        self.assertIsNone(extract_details(html,'phongtro123','https://phongtro123.com/another-pr456.html')['publisher_name'])

if __name__=='__main__':unittest.main()
