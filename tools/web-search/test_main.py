from __future__ import annotations

import json
import os
import unittest
from datetime import datetime, timezone
from io import BytesIO
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.parse import parse_qs, urlsplit

import main


class FakeResponse:
    def __init__(self, body: bytes, status: int = 200):
        self.body = body
        self.status = status

    def read(self, maximum: int = -1) -> bytes:
        return self.body if maximum < 0 else self.body[:maximum]

    def __enter__(self) -> "FakeResponse":
        return self

    def __exit__(self, exception_type, exception, traceback) -> None:
        return None


class WebSearchTests(unittest.TestCase):
    def setUp(self) -> None:
        self.environment = patch.dict(
            os.environ,
            {"LATTICE_SEARXNG_URL": "https://search.example"},
            clear=True,
        )
        self.environment.start()
        self.addCleanup(self.environment.stop)

    def test_returns_timestamped_results_with_source_attribution(self) -> None:
        payload = {
            "results": [
                {
                    "title": "First result",
                    "url": "https://example.org/first",
                    "content": "  A useful   summary. ",
                    "engine": "example",
                    "publishedDate": "2026-11-09T00:00:00",
                },
                {
                    "title": "Second result",
                    "url": "https://example.org/second",
                    "content": "Another summary.",
                },
            ],
            "unresponsive_engines": [],
        }
        observed_request = {}

        def opener(request, timeout):
            observed_request["url"] = request.full_url
            observed_request["timeout"] = timeout
            observed_request["accept"] = request.get_header("Accept")
            return FakeResponse(json.dumps(payload).encode("utf-8"))

        result = main.search(
            {"query": "  lattice reasoning  "},
            opener=opener,
            clock=lambda: datetime(2026, 11, 10, 18, 30, tzinfo=timezone.utc),
        )

        parsed_url = urlsplit(observed_request["url"])
        query = parse_qs(parsed_url.query)
        self.assertEqual("/search", parsed_url.path)
        self.assertEqual(["lattice reasoning"], query["q"])
        self.assertEqual(["json"], query["format"])
        self.assertEqual(["1"], query["safesearch"])
        self.assertEqual(4, observed_request["timeout"])
        self.assertEqual("application/json", observed_request["accept"])
        self.assertIn("Retrieved at: 2026-11-10T18:30:00Z", result)
        self.assertIn("1. First result", result)
        self.assertIn("Source: https://example.org/first", result)
        self.assertIn("Search engine: example", result)
        self.assertIn("Published date (reported): 2026-11-09T00:00:00", result)
        self.assertIn("A useful summary.", result)
        self.assertIn("2. Second result", result)

    def test_honors_configured_instance_path_and_result_limit(self) -> None:
        os.environ["LATTICE_SEARXNG_URL"] = "https://search.example/searx"
        payload = {
            "results": [
                {"title": f"Result {index}", "url": f"https://example.org/{index}"}
                for index in range(1, 5)
            ]
        }
        observed_url = {}

        def opener(request, timeout):
            observed_url["value"] = request.full_url
            return FakeResponse(json.dumps(payload).encode("utf-8"))

        result = main.search(
            {"query": "lattice", "max_results": 2},
            opener=opener,
            clock=lambda: datetime(2026, 11, 10, tzinfo=timezone.utc),
        )

        self.assertIn("/searx/search?", observed_url["value"])
        self.assertIn("Results: 2", result)
        self.assertIn("1. Result 1", result)
        self.assertIn("2. Result 2", result)
        self.assertNotIn("3. Result 3", result)

    def test_filters_unsafe_duplicate_and_incomplete_result_links(self) -> None:
        payload = {
            "results": [
                {"title": "Unsafe", "url": "javascript:alert(1)"},
                {"title": "Missing target"},
                {"title": "First", "url": "https://example.org/page"},
                {"title": "Duplicate", "url": "https://example.org/page"},
                {"title": "Credentials", "url": "https://user:pass@example.org/page"},
                {"title": "Malformed", "url": "https://[broken"},
            ]
        }

        result = main.search(
            {"query": "lattice"},
            opener=lambda request, timeout: FakeResponse(json.dumps(payload).encode("utf-8")),
            clock=lambda: datetime(2026, 11, 10, tzinfo=timezone.utc),
        )

        self.assertIn("Results: 1", result)
        self.assertIn("1. First", result)
        self.assertNotIn("Unsafe", result)
        self.assertNotIn("Duplicate", result)
        self.assertNotIn("Credentials", result)
        self.assertNotIn("Malformed", result)

    def test_reports_empty_results_without_claiming_the_topic_is_false(self) -> None:
        payload = {
            "results": [],
            "unresponsive_engines": [["example", "timeout"]],
        }

        result = main.search(
            {"query": "an obscure topic"},
            opener=lambda request, timeout: FakeResponse(json.dumps(payload).encode("utf-8")),
            clock=lambda: datetime(2026, 11, 10, tzinfo=timezone.utc),
        )

        self.assertIn("No search results were returned.", result)
        self.assertIn("Search engines that did not respond: example", result)

    def test_rejects_missing_or_invalid_instance_configuration(self) -> None:
        del os.environ["LATTICE_SEARXNG_URL"]
        with self.assertRaisesRegex(ValueError, "Set LATTICE_SEARXNG_URL"):
            main.search({"query": "lattice"}, opener=lambda request, timeout: None)

        for invalid in (
            "file:///tmp/search",
            "https://user:pass@example.org",
            "https://example.org/?token=value",
            "https://example.org/#fragment",
        ):
            with self.subTest(invalid=invalid):
                os.environ["LATTICE_SEARXNG_URL"] = invalid
                with self.assertRaises(ValueError):
                    main.search({"query": "lattice"}, opener=lambda request, timeout: None)

    def test_explains_when_json_format_is_disabled(self) -> None:
        def opener(request, timeout):
            raise HTTPError(request.full_url, 403, "Forbidden", {}, BytesIO())

        with self.assertRaisesRegex(ValueError, "Enable 'json' under search.formats"):
            main.search({"query": "lattice"}, opener=opener)

    def test_rejects_invalid_query_and_result_limits(self) -> None:
        invalid_arguments = (
            {"query": ""},
            {"query": "line\nbreak"},
            {"query": "x" * (main.MAX_QUERY_LENGTH + 1)},
            {"query": "lattice", "max_results": 0},
            {"query": "lattice", "max_results": 9},
            {"query": "lattice", "max_results": True},
            {"query": "lattice", "max_results": 1.5},
        )

        for arguments in invalid_arguments:
            with self.subTest(arguments=arguments):
                with self.assertRaises(ValueError):
                    main.read_arguments(arguments)

    def test_rejects_oversized_or_non_json_responses(self) -> None:
        with self.assertRaisesRegex(ValueError, "one-megabyte limit"):
            main.search(
                {"query": "lattice"},
                opener=lambda request, timeout: FakeResponse(
                    b"x" * (main.MAX_RESPONSE_BYTES + 1)
                ),
            )

        with self.assertRaisesRegex(ValueError, "valid UTF-8 JSON"):
            main.search(
                {"query": "lattice"},
                opener=lambda request, timeout: FakeResponse(b"<html>blocked</html>"),
            )


if __name__ == "__main__":
    unittest.main()
