from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit, urlunsplit
from urllib.request import Request, urlopen
from typing import Any, Callable

PROTOCOL_VERSION = 1
MAX_QUERY_LENGTH = 300
DEFAULT_MAX_RESULTS = 5
MAX_RESULTS = 8
MAX_RESPONSE_BYTES = 1024 * 1024
REQUEST_TIMEOUT_SECONDS = 4
USER_AGENT = "Lattice-Web-Search/1.0"


def clean_text(value: Any, maximum_length: int) -> str:
    if not isinstance(value, str):
        return ""

    normalized = " ".join(value.split())
    return normalized[:maximum_length]


def read_instance_url() -> tuple[str, str]:
    configured = os.environ.get("LATTICE_SEARXNG_URL", "").strip()
    if not configured:
        raise ValueError(
            "Web search is not configured. Set LATTICE_SEARXNG_URL to a SearXNG instance URL."
        )

    if len(configured) > 2048:
        raise ValueError("LATTICE_SEARXNG_URL exceeds the supported URL length.")

    parsed = urlsplit(configured)
    if (
        parsed.scheme.lower() not in ("http", "https")
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError(
            "LATTICE_SEARXNG_URL must be an HTTP(S) instance URL without credentials, query, or fragment."
        )

    path = parsed.path.rstrip("/")
    instance = urlunsplit((parsed.scheme.lower(), parsed.netloc, path, "", ""))
    endpoint = instance if path.lower().endswith("/search") else instance + "/search"
    return endpoint, instance


def read_arguments(arguments: Any) -> tuple[str, int]:
    if not isinstance(arguments, dict):
        raise ValueError("'arguments' must be an object.")

    query = arguments.get("query")
    if not isinstance(query, str):
        raise ValueError("'query' must be a string.")

    query = query.strip()
    if not query:
        raise ValueError("'query' cannot be empty.")
    if len(query) > MAX_QUERY_LENGTH:
        raise ValueError(f"'query' cannot exceed {MAX_QUERY_LENGTH} characters.")
    if any(ord(character) < 32 or ord(character) == 127 for character in query):
        raise ValueError("'query' cannot contain control characters.")

    maximum = arguments.get("max_results", DEFAULT_MAX_RESULTS)
    if isinstance(maximum, bool) or not isinstance(maximum, int):
        raise ValueError("'max_results' must be a whole number.")
    if maximum < 1 or maximum > MAX_RESULTS:
        raise ValueError(f"'max_results' must be between 1 and {MAX_RESULTS}.")

    return query, maximum


def safe_result_url(value: Any) -> str:
    if not isinstance(value, str) or len(value) > 2048:
        return ""

    candidate = value.strip()

    try:
        parsed = urlsplit(candidate)
        hostname = parsed.hostname
    except ValueError:
        return ""

    if (
        parsed.scheme.lower() not in ("http", "https")
        or not hostname
        or parsed.username is not None
        or parsed.password is not None
    ):
        return ""

    return candidate


def format_observation(
    query: str,
    instance: str,
    retrieved_at: str,
    results: list[dict[str, str]],
    unresponsive_engines: list[str],
) -> str:
    lines = [
        f"Query: {query}",
        f"Search provider: SearXNG ({instance})",
        f"Retrieved at: {retrieved_at}",
    ]

    if not results:
        lines.append("No search results were returned.")
    else:
        lines.append(f"Results: {len(results)}")

        for index, result in enumerate(results, start=1):
            lines.append(f"{index}. {result['title']}")
            lines.append(f"   Source: {result['url']}")
            if result["engine"]:
                lines.append(f"   Search engine: {result['engine']}")
            if result["published"]:
                lines.append(f"   Published date (reported): {result['published']}")
            if result["snippet"]:
                lines.append(f"   {result['snippet']}")

    if unresponsive_engines:
        lines.append(
            "Search engines that did not respond: " + ", ".join(unresponsive_engines)
        )

    return "\n".join(lines)


def search(
    arguments: Any,
    opener: Callable[..., Any] | None = None,
    clock: Callable[[], datetime] | None = None,
) -> str:
    query, maximum = read_arguments(arguments)
    endpoint, instance = read_instance_url()
    request_url = endpoint + "?" + urlencode(
        {
            "q": query,
            "format": "json",
            "safesearch": "1",
        }
    )
    request = Request(
        request_url,
        headers={
            "Accept": "application/json",
            "User-Agent": USER_AGENT,
        },
    )

    open_request = opener or urlopen

    try:
        with open_request(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
            status = getattr(response, "status", 200)
            if status != 200:
                raise ValueError(f"The configured SearXNG instance returned HTTP {status}.")

            payload_bytes = response.read(MAX_RESPONSE_BYTES + 1)
    except HTTPError as error:
        if error.code == 403:
            raise ValueError(
                "The SearXNG instance does not expose JSON search results. "
                "Enable 'json' under search.formats or use another instance."
            ) from error
        raise ValueError(
            f"The SearXNG instance returned HTTP {error.code}."
        ) from error
    except (URLError, TimeoutError, OSError) as error:
        raise ValueError(
            "The SearXNG instance could not be reached. Check its URL and network availability."
        ) from error

    if len(payload_bytes) > MAX_RESPONSE_BYTES:
        raise ValueError("The SearXNG response exceeded the one-megabyte limit.")

    try:
        payload = json.loads(payload_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError("The SearXNG instance did not return valid UTF-8 JSON.") from error

    if not isinstance(payload, dict) or not isinstance(payload.get("results"), list):
        raise ValueError("The SearXNG response did not contain a results array.")

    results: list[dict[str, str]] = []
    seen_urls: set[str] = set()

    for item in payload["results"]:
        if not isinstance(item, dict):
            continue

        title = clean_text(item.get("title"), 400)
        url = safe_result_url(item.get("url"))

        if not title or not url or url in seen_urls:
            continue

        seen_urls.add(url)
        results.append(
            {
                "title": title,
                "url": url,
                "engine": clean_text(item.get("engine"), 100),
                "published": clean_text(item.get("publishedDate"), 80),
                "snippet": clean_text(item.get("content"), 1200),
            }
        )

        if len(results) >= maximum:
            break

    raw_unresponsive = payload.get("unresponsive_engines", [])
    unresponsive = []
    if isinstance(raw_unresponsive, list):
        for item in raw_unresponsive:
            if isinstance(item, (list, tuple)) and item:
                engine = clean_text(item[0], 100)
            else:
                engine = clean_text(item, 100)
            if engine and engine not in unresponsive:
                unresponsive.append(engine)

    current_time = (clock or (lambda: datetime.now(timezone.utc)))()
    if current_time.tzinfo is None:
        raise ValueError("The search clock must provide a timezone-aware datetime.")
    retrieved_at = current_time.astimezone(timezone.utc).isoformat(timespec="seconds").replace(
        "+00:00", "Z"
    )

    return format_observation(
        query,
        instance,
        retrieved_at,
        results,
        unresponsive,
    )


def write_success(request_id: Any, result: str) -> None:
    response = {
        "protocolVersion": PROTOCOL_VERSION,
        "requestId": request_id,
        "success": True,
        "result": result,
    }
    sys.stdout.write(json.dumps(response, ensure_ascii=False, separators=(",", ":")) + "\n")


def write_failure(request_id: Any, message: str) -> None:
    response = {
        "protocolVersion": PROTOCOL_VERSION,
        "requestId": request_id,
        "success": False,
        "error": message,
    }
    sys.stdout.write(json.dumps(response, ensure_ascii=False, separators=(",", ":")) + "\n")


def main() -> None:
    request_id: Any = None

    try:
        line = sys.stdin.readline()
        if not line:
            raise ValueError("No request was supplied.")

        request = json.loads(line)
        if not isinstance(request, dict):
            raise ValueError("The request must be a JSON object.")

        request_id = request.get("requestId")
        if request.get("protocolVersion") != PROTOCOL_VERSION:
            raise ValueError("Unsupported protocol version.")
        if request.get("operation") != "execute":
            raise ValueError("Unsupported operation.")

        write_success(request_id, search(request.get("arguments")))
    except (json.JSONDecodeError, ValueError, TypeError) as error:
        write_failure(request_id, str(error))
    except Exception:
        write_failure(request_id, "The web search tool encountered an unexpected error.")


if __name__ == "__main__":
    main()
