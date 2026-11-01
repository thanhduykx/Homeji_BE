import json
import unittest
from enrich_rental_source_details import extract_details,publisher

class SourceDetailTests(unittest.TestCase):
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
