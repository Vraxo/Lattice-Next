from __future__ import annotations

import unittest
from datetime import datetime, timezone
from unittest.mock import patch

import main


class FakeSearchClient:
    def __init__(self, results, timeout=None, error=None):
        self.results = results
        self.timeout = timeout
        self.error = error
        self.request = None

    def text(self, query, **options):
        self.request = {"query": query, **options}
        if self.error is not None:
            raise self.error
        return self.results


class WebSearchTests(unittest.TestCase):
    def test_searches_without_service_url_or_api_key_and_attributes_results(self) -> None:
        results = [
            {
                "title": "First result",
                "href": "https://example.org/first",
                "body": "  A useful   summary. ",
            },
            {
                "title": "Second result",
                "href": "https://example.net/second",
                "body": "Another summary.",
            },
        ]
        client = FakeSearchClient(results)

        def factory(timeout):
            client.timeout = timeout
            return client

        observation = main.search(
            {"query": "  lattice reasoning  "},
            search_client_factory=factory,
            clock=lambda: datetime(2026, 11, 10, 18, 30, tzinfo=timezone.utc),
        )

        self.assertEqual(main.REQUEST_TIMEOUT_SECONDS, client.timeout)
        self.assertEqual(
            {
                "query": "lattice reasoning",
                "max_results": main.DEFAULT_MAX_RESULTS,
                "safesearch": "moderate",
                "backend": "auto",
            },
            client.request,
        )
        self.assertIn("Search provider: DDGS metasearch", observation)
        self.assertIn("Retrieved at: 2026-11-10T18:30:00Z", observation)
        self.assertIn("unverified external content", observation)
        self.assertIn("1. First result", observation)
        self.assertIn("Source: https://example.org/first", observation)
        self.assertIn("Domain: example.org", observation)
        self.assertIn("A useful summary.", observation)
        self.assertIn("2. Second result", observation)

    def test_honors_result_limit(self) -> None:
        results = [
            {
                "title": f"Result {index}",
                "href": f"https://example.org/{index}",
                "body": "Summary",
            }
            for index in range(1, 5)
        ]
        client = FakeSearchClient(results)

        observation = main.search(
            {"query": "lattice", "max_results": 2},
            search_client_factory=lambda timeout: client,
            clock=lambda: datetime(2026, 11, 10, tzinfo=timezone.utc),
        )

        self.assertEqual(2, client.request["max_results"])
        self.assertIn("Results: 2", observation)
        self.assertIn("1. Result 1", observation)
        self.assertIn("2. Result 2", observation)
        self.assertNotIn("3. Result 3", observation)

    def test_filters_unsafe_duplicate_and_incomplete_result_links(self) -> None:
        results = [
            {"title": "Unsafe", "href": "javascript:alert(1)"},
            {"title": "Missing target"},
            {"title": "First", "href": "https://example.org/page"},
            {"title": "Duplicate", "href": "https://example.org/page"},
            {"title": "Credentials", "href": "https://user:pass@example.org/page"},
            {"title": "Malformed", "href": "https://[broken"},
        ]

        observation = main.search(
            {"query": "lattice"},
            search_client_factory=lambda timeout: FakeSearchClient(results),
            clock=lambda: datetime(2026, 11, 10, tzinfo=timezone.utc),
        )

        self.assertIn("Results: 1", observation)
        self.assertIn("1. First", observation)
        self.assertNotIn("Unsafe", observation)
        self.assertNotIn("Duplicate", observation)
        self.assertNotIn("Credentials", observation)
        self.assertNotIn("Malformed", observation)

    def test_reports_empty_results_without_treating_them_as_negative_evidence(self) -> None:
        observation = main.search(
            {"query": "an obscure topic"},
            search_client_factory=lambda timeout: FakeSearchClient([]),
            clock=lambda: datetime(2026, 11, 10, tzinfo=timezone.utc),
        )

        self.assertIn("No search results were returned.", observation)
        self.assertIn("unverified external content", observation)

    def test_reports_a_clear_error_when_the_optional_dependency_is_missing(self) -> None:
        with patch.object(main, "DDGS", None):
            with self.assertRaisesRegex(ValueError, "requirements.txt"):
                main.search({"query": "lattice"})

    def test_reports_temporary_provider_failures_without_exposing_provider_errors(self) -> None:
        client = FakeSearchClient([], error=RuntimeError("internal network detail"))

        with self.assertRaisesRegex(ValueError, "temporarily unavailable or rate-limiting"):
            main.search(
                {"query": "lattice"},
                search_client_factory=lambda timeout: client,
            )

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

    def test_requires_timezone_aware_retrieval_time(self) -> None:
        with self.assertRaisesRegex(ValueError, "timezone-aware"):
            main.search(
                {"query": "lattice"},
                search_client_factory=lambda timeout: FakeSearchClient([]),
                clock=lambda: datetime(2026, 11, 10),
            )


if __name__ == "__main__":
    unittest.main()
