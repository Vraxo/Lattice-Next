# Web page fetch

A bounded, read-only external tool for retrieving public HTML and plain-text pages. It is separate from web search: search finds candidate URLs; this tool reads one URL that the user or another trusted workflow selected.

## Safety and limits

- HTTP/HTTPS only, standard ports only, no URL credentials.
- DNS results are checked; every resolved address must be globally routable. The connection is pinned to a validated IP while preserving the original host for HTTP Host and TLS certificate verification.
- Every redirect is revalidated, redirects are limited to three, and HTTPS-to-HTTP downgrades are rejected.
- Five-second connection timeout, 1 MB response-body limit, and 12,000-character extracted-text limit.
- HTML and plain text only; compressed responses and other media types are rejected.
- Scripts, styles, SVG, templates, and noscript content are excluded from HTML text extraction.
- No cookies, authentication, POST requests, arbitrary request headers, or proxy environment settings are used.
- The output includes the final URL and UTC retrieval timestamp. Extracted content is explicitly labelled untrusted external text; it is not converted into knowledge or treated as instructions.

These checks reduce exposure to unsafe destinations but are not an OS sandbox. The tool runs with the current user's network permissions and requires the ordinary per-tool read permission approval.
