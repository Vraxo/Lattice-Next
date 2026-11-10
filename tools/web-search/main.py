from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from urllib.parse import urlsplit
from typing import Any, Callable

try:
    from ddgs import DDGS
except ImportError:
    DDGS = None

PROTOCOL_VERSION = 1
MAX_QUERY_LENGTH = 300
DEFAULT_MAX_RESULTS = 5
MAX_RESULTS = 8
MAX_TITLE_LENGTH = 400
MAX_SNIPPET_LENGTH = 1200
MAX_URL_LENGTH = 2048
REQUEST_TIMEOUT_SECONDS = 5


def clean_text(value: Any, maximum_length: int) -> str:
    if not isinstance(value, str):
        return ""

    return " ".join(value.split())[:maximum_length]


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
    if not isinstance(value, str) or len(value) > MAX_URL_LENGTH:
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


def normalize_results(raw_results: Any, maximum: int) -> list[dict[str, str]]:
    if not isinstance(raw_results, list):
        raise ValueError("The search provider returned an invalid results collection.")

    results: list[dict[str, str]] = []
    seen_urls: set[str] = set()

    for item in raw_results:
        if not isinstance(item, dict):
            continue

        title = clean_text(item.get("title"), MAX_TITLE_LENGTH)
        url = safe_result_url(item.get("href", item.get("url")))

        if not title or not url or url in seen_urls:
            continue

        try:
            domain = urlsplit(url).hostname or ""
        except ValueError:
            continue

        seen_urls.add(url)
        results.append(
            {
                "title": title,
                "url": url,
                "domain": domain,
                "snippet": clean_text(
                    item.get("body", item.get("snippet")),
                    MAX_SNIPPET_LENGTH,
                ),
            }
        )

        if len(results) >= maximum:
            break

    return results


def format_observation(
    query: str,
    retrieved_at: str,
    results: list[dict[str, str]],
) -> str:
    lines = [
        f"Query: {query}",
        "Search provider: DDGS metasearch (automatic backend selection)",
        f"Retrieved at: {retrieved_at}",
        "Note: Search snippets are unverified external content, not established facts.",
    ]

    if not results:
        lines.append("No search results were returned.")
    else:
        lines.append(f"Results: {len(results)}")

        for index, result in enumerate(results, start=1):
            lines.append(f"{index}. {result['title']}")
            lines.append(f"   Source: {result['url']}")
            lines.append(f"   Domain: {result['domain']}")
            if result["snippet"]:
                lines.append(f"   {result['snippet']}")

    return "\n".join(lines)


def search(
    arguments: Any,
    search_client_factory: Callable[..., Any] | None = None,
    clock: Callable[[], datetime] | None = None,
) -> str:
    query, maximum = read_arguments(arguments)
    client_factory = search_client_factory or DDGS

    if client_factory is None:
        raise ValueError(
            "The web-search dependency is missing. Install it with "
            "'python -m pip install -r tools/web-search/requirements.txt'."
        )

    try:
        client = client_factory(timeout=REQUEST_TIMEOUT_SECONDS)
        raw_results = client.text(
            query,
            max_results=maximum,
            safesearch="moderate",
            backend="auto",
        )
    except Exception as error:
        raise ValueError(
            "The public search providers could not complete the request. "
            "They may be temporarily unavailable or rate-limiting automated searches."
        ) from error

    results = normalize_results(raw_results, maximum)
    current_time = (clock or (lambda: datetime.now(timezone.utc)))()

    if current_time.tzinfo is None:
        raise ValueError("The search clock must provide a timezone-aware datetime.")

    retrieved_at = (
        current_time.astimezone(timezone.utc)
        .isoformat(timespec="seconds")
        .replace("+00:00", "Z")
    )

    return format_observation(query, retrieved_at, results)


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
