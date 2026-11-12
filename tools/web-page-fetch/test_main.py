from __future__ import annotations

import socket
import unittest
from datetime import datetime, timezone

import main


class FakeResponse:
    def __init__(self, status=200, headers=None, body=b"") -> None:
        self.status = status
        self.headers = headers or {}
        self.body = body

    def getheader(self, name, default=None):
        return self.headers.get(name, default)

    def read(self, maximum):
        return self.body[:maximum]


class FakeConnection:
    def __init__(self, response) -> None:
        self.response = response
        self.request_data = None
        self.closed = False

    def request(self, method, target, headers=None):
        self.request_data = (method, target, headers or {})

    def getresponse(self):
        return self.response

    def close(self):
        self.closed = True


def public_resolver(host, port, type):
    return [(socket.AF_INET, socket.SOCK_STREAM, 6, "", ("8.8.8.8", port))]


class PageFetchTests(unittest.TestCase):
    def test_fetches_html_with_a_pinned_public_address_and_extracts_visible_text(self) -> None:
        response = FakeResponse(
            headers={"Content-Type": "text/html; charset=utf-8", "Cache-Control": "max-age=3600"},
            body=(
                b"<html><head><title>Example page</title>"
                b"<script>ignore this secret</script><style>.x{}</style></head>"
                b"<body><h1>Welcome</h1><p>A useful <b>summary</b>.</p>"
                b"<svg>hidden vector text</svg></body></html>"
            ),
        )
        connection = FakeConnection(response)
        captured = {}

        def factory(scheme, hostname, port, address, timeout):
            captured.update(
                scheme=scheme,
                hostname=hostname,
                port=port,
                address=address,
                timeout=timeout,
            )
            return connection

        observation = main.fetch(
            {"url": "https://Example.org/path?q=1"},
            resolver=public_resolver,
            connection_factory=factory,
            clock=lambda: datetime(2026, 11, 12, 7, 0, tzinfo=timezone.utc),
        )

        self.assertEqual(
            {
                "scheme": "https",
                "hostname": "example.org",
                "port": 443,
                "address": "8.8.8.8",
                "timeout": main.REQUEST_TIMEOUT_SECONDS,
            },
            captured,
        )
        self.assertEqual(("GET", "/path?q=1"), connection.request_data[:2])
        self.assertEqual("example.org", connection.request_data[2]["Host"])
        self.assertTrue(connection.closed)
        self.assertIn("Final URL: https://Example.org/path?q=1", observation)
        self.assertIn("Retrieved at: 2026-11-12T07:00:00Z", observation)
        self.assertIn("Fresh until: 2026-11-12T08:00:00Z", observation)
        self.assertIn("Page title: Example page", observation)
        self.assertIn("Welcome A useful summary.", observation)
        self.assertIn("untrusted external text", observation)
        self.assertNotIn("ignore this secret", observation)
        self.assertNotIn("hidden vector text", observation)

    def test_uses_age_and_expires_headers_to_bound_freshness(self) -> None:
        retrieved_at = datetime(2026, 11, 12, 7, 0, tzinfo=timezone.utc)
        max_age = FakeResponse(headers={"Cache-Control": "public, max-age=3600", "Age": "600"})
        expires = FakeResponse(headers={"Expires": "Thu, 12 Nov 2026 08:30:00 GMT"})
        no_cache = FakeResponse(headers={"Cache-Control": "no-cache"})

        self.assertEqual(
            datetime(2026, 11, 12, 7, 50, tzinfo=timezone.utc),
            main.get_fresh_until(max_age, retrieved_at),
        )
        self.assertEqual(
            datetime(2026, 11, 12, 8, 30, tzinfo=timezone.utc),
            main.get_fresh_until(expires, retrieved_at),
        )
        self.assertEqual(retrieved_at, main.get_fresh_until(no_cache, retrieved_at))
        self.assertIsNone(main.get_fresh_until(FakeResponse(), retrieved_at))

    def test_fetches_plain_text_and_reports_the_final_url(self) -> None:
        responses = [
            FakeResponse(302, {"Location": "/article"}),
            FakeResponse(200, {"Content-Type": "text/plain; charset=utf-8"}, b"Current public information."),
        ]
        connections = []
        factory_calls = []

        def factory(scheme, hostname, port, address, timeout):
            factory_calls.append((scheme, hostname, port, address, timeout))
            connection = FakeConnection(responses[len(connections)])
            connections.append(connection)
            return connection

        observation = main.fetch(
            {"url": "https://example.org/start"},
            resolver=public_resolver,
            connection_factory=factory,
            clock=lambda: datetime(2026, 11, 12, 7, 1, tzinfo=timezone.utc),
        )

        self.assertEqual(2, len(connections))
        self.assertEqual("Current public information.", observation.splitlines()[-1])
        self.assertIn("Final URL: https://example.org/article", observation)
        self.assertIn("Retrieved at: 2026-11-12T07:01:00Z", observation)

    def test_rejects_non_http_schemes_credentials_and_nonstandard_ports(self) -> None:
        invalid_urls = (
            "file:///etc/passwd",
            "ftp://example.org/file",
            "https://user:pass@example.org/",
            "https://example.org:8443/",
            "http://example.org:443/",
            "https://example.org/" + ("x" * main.MAX_URL_LENGTH),
        )

        for url in invalid_urls:
            with self.subTest(url=url):
                with self.assertRaises(ValueError):
                    main.validate_url(url)

    def test_rejects_private_or_mixed_dns_answers_before_connecting(self) -> None:
        resolvers = (
            lambda host, port, type: [(socket.AF_INET, socket.SOCK_STREAM, 6, "", ("127.0.0.1", port))],
            lambda host, port, type: [
                (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("8.8.8.8", port)),
                (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("10.0.0.1", port)),
            ],
        )

        for resolver in resolvers:
            with self.subTest(resolver=resolver):
                with self.assertRaisesRegex(ValueError, "non-public"):
                    main.resolve_public_address("example.org", 443, resolver)

    def test_rejects_loopback_link_local_and_private_ip_literals(self) -> None:
        for address in ("127.0.0.1", "169.254.169.254", "10.0.0.1", "192.168.1.2", "::1", "fc00::1"):
            with self.subTest(address=address):
                with self.assertRaisesRegex(ValueError, "non-public"):
                    main.resolve_public_address(address, 443)

    def test_revalidates_redirect_targets_and_rejects_private_destinations(self) -> None:
        connection = FakeConnection(FakeResponse(302, {"Location": "https://127.0.0.1/admin"}))
        factory_calls = []

        def factory(*arguments):
            factory_calls.append(arguments)
            return connection

        with self.assertRaisesRegex(ValueError, "non-public"):
            main.fetch(
                {"url": "https://example.org/start"},
                resolver=public_resolver,
                connection_factory=factory,
            )

        self.assertEqual(1, len(factory_calls))
        self.assertTrue(connection.closed)

    def test_rejects_https_downgrade_redirects(self) -> None:
        connection = FakeConnection(FakeResponse(302, {"Location": "http://example.org/insecure"}))
        with self.assertRaisesRegex(ValueError, "insecure HTTP"):
            main.fetch(
                {"url": "https://example.org/start"},
                resolver=public_resolver,
                connection_factory=lambda *arguments: connection,
            )

    def test_enforces_redirect_limit(self) -> None:
        connections = []

        def factory(*arguments):
            connection = FakeConnection(FakeResponse(302, {"Location": "/again"}))
            connections.append(connection)
            return connection

        with self.assertRaisesRegex(ValueError, "redirect limit"):
            main.fetch(
                {"url": "https://example.org/start"},
                resolver=public_resolver,
                connection_factory=factory,
            )

        self.assertEqual(main.MAX_REDIRECTS + 1, len(connections))
        self.assertTrue(all(connection.closed for connection in connections))

    def test_rejects_non_text_content_and_compressed_responses(self) -> None:
        responses = (
            FakeResponse(headers={"Content-Type": "application/pdf"}, body=b"%PDF"),
            FakeResponse(
                headers={"Content-Type": "text/html", "Content-Encoding": "gzip"},
                body=b"compressed",
            ),
        )

        for response in responses:
            connection = FakeConnection(response)
            with self.subTest(response=response.headers):
                with self.assertRaises(ValueError):
                    main.fetch(
                        {"url": "https://example.org/"},
                        resolver=public_resolver,
                        connection_factory=lambda *arguments: connection,
                    )

    def test_rejects_oversized_page_bodies(self) -> None:
        connection = FakeConnection(
            FakeResponse(
                headers={"Content-Type": "text/plain"},
                body=b"x" * (main.MAX_BODY_BYTES + 1),
            )
        )
        with self.assertRaisesRegex(ValueError, "response limit"):
            main.fetch(
                {"url": "https://example.org/"},
                resolver=public_resolver,
                connection_factory=lambda *arguments: connection,
            )

    def test_rejects_non_success_http_status(self) -> None:
        connection = FakeConnection(FakeResponse(503, body=b"unavailable"))
        with self.assertRaisesRegex(ValueError, "HTTP status 503"):
            main.fetch(
                {"url": "https://example.org/"},
                resolver=public_resolver,
                connection_factory=lambda *arguments: connection,
            )

    def test_rejects_naive_retrieval_timestamps(self) -> None:
        connection = FakeConnection(
            FakeResponse(headers={"Content-Type": "text/plain"}, body=b"Page text")
        )
        with self.assertRaisesRegex(ValueError, "timezone-aware"):
            main.fetch(
                {"url": "https://example.org/"},
                resolver=public_resolver,
                connection_factory=lambda *arguments: connection,
                clock=lambda: datetime(2026, 11, 12),
            )

    def test_rejects_empty_page_text(self) -> None:
        connection = FakeConnection(
            FakeResponse(
                headers={"Content-Type": "text/html"},
                body=b"<html><head><script>hidden</script></head><body></body></html>",
            )
        )
        with self.assertRaisesRegex(ValueError, "readable text"):
            main.fetch(
                {"url": "https://example.org/"},
                resolver=public_resolver,
                connection_factory=lambda *arguments: connection,
            )


if __name__ == "__main__":
    unittest.main()
