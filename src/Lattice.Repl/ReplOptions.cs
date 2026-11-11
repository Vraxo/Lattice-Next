namespace Lattice.Repl;

internal sealed record ReplOptions(string? ServerUrl, bool ShowHelp)
{
    public const string Usage = """
        Lattice.Repl — interactive Lattice frontend

        Usage:
          lattice-repl [--server <url>]
          lattice-repl --help

        Options:
          --server <url>  Use a Lattice HTTP server instead of the in-process runtime.
          --help, -h      Show this help.

        Set LATTICE_SERVER_API_KEY when the server requires bearer-token authentication.
        Local tools and LM Studio translation remain configured on this machine.
        """;

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ReplOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        string? serverUrl = null;
        var showHelp = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;

                case "--server":
                    if (serverUrl is not null ||
                        index + 1 >= arguments.Count ||
                        string.IsNullOrWhiteSpace(arguments[index + 1]))
                    {
                        error = "The --server option requires one absolute HTTP or HTTPS URL.";
                        return false;
                    }

                    var requestedUrl = arguments[++index];

                    if (!Uri.TryCreate(requestedUrl, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                        !string.IsNullOrEmpty(uri.Query) ||
                        !string.IsNullOrEmpty(uri.Fragment))
                    {
                        error = "The --server option requires an absolute HTTP or HTTPS URL without a query or fragment.";
                        return false;
                    }

                    serverUrl = requestedUrl;
                    break;

                default:
                    error = $"Unknown option '{arguments[index]}'.";
                    return false;
            }
        }

        options = new ReplOptions(serverUrl, showHelp);
        return true;
    }
}
