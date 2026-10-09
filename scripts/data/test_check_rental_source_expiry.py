import unittest
from check_rental_source_expiry import source_identity, checked_expiry
import test_muaban_sources as muaban_fixtures


class SourceExpiryCheckerTests(unittest.TestCase):
    def test_only_source_ad_urls_can_be_checked(self):
        self.assertEqual("123", source_identity("https://phongtro123.com/tin-pr123.html"))
        self.assertEqual("123", source_identity("https://muaban.net/bat-dong-san/phong-id123"))
        for url in ["http://phongtro123.com/tin-pr123.html", "https://evil.invalid/tin-pr123.html",
                    "https://phongtro123.com:444/tin-pr123.html", "https://phongtro123.com:bad/tin-pr123.html",
                    "https://user@phongtro123.com/tin-pr123.html", "https://phongtro123.com/tin-pr123.html?token=x",
                    "https://phongtro123.com/tin-pr123.html#fragment", "https://phongtro123.com/"]:
            self.assertIsNone(source_identity(url), url)

    def test_muaban_deadline_comes_from_requested_ad_and_requires_timezone(self):
        url = "https://muaban.net/bat-dong-san/phong-id123"
        fixture = muaban_fixtures.MuabanParserTests()
        self.assertEqual("2026-09-12T03:55:00+00:00", checked_expiry(fixture.fixture(service_end="2026-09-12T10:55:00+07:00"), url))
        self.assertIsNone(checked_expiry(fixture.fixture(service_end="2026-09-12T10:55:00"), url))
        with self.assertRaises(ValueError):
            checked_expiry(fixture.fixture(id=999), url)


if __name__ == "__main__":
    unittest.main()
