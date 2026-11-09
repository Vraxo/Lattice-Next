namespace Lattice.Translators.LmStudio;

public sealed record LmStudioConfiguration
{
    public string ApiUrl { get; init; } = "http://127.0.0.1:1234/v1/chat/completions";

    public string? ModelKey { get; init; }

    public string? ModelIdentifier { get; init; }

    public string LmsExecutable { get; init; } = "lms";

    public string? ApiToken { get; init; }

    public int ServerStartupTimeoutSeconds { get; init; } = 60;

    public int ModelLoadTimeoutSeconds { get; init; } = 180;

    public int RequestTimeoutSeconds { get; init; } = 120;

    public Uri GetServerBaseAddress()
    {
        if (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out var apiUri) ||
            apiUri.Scheme != Uri.UriSchemeHttp ||
            !apiUri.IsLoopback ||
            !string.IsNullOrEmpty(apiUri.Query) ||
            !string.IsNullOrEmpty(apiUri.Fragment))
        {
            throw new InvalidOperationException(
                "LM Studio apiUrl must be a loopback HTTP URL without a query or fragment.");
        }

        var path = apiUri.AbsolutePath.TrimEnd('/');

        if (path.Length > 0 && !string.Equals(
            path,
            "/v1/chat/completions",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "LM Studio apiUrl must be the server root or end in /v1/chat/completions.");
        }

        return new UriBuilder(apiUri)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }

    public bool TryValidate(out string? error)
    {
        error = null;

        try
        {
            _ = GetServerBaseAddress();
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return false;
        }

        if (string.IsNullOrWhiteSpace(LmsExecutable))
        {
            error = "LM Studio lmsExecutable cannot be empty.";
            return false;
        }

        if (ServerStartupTimeoutSeconds is < 1 or > 300)
        {
            error = "LM Studio serverStartupTimeoutSeconds must be between 1 and 300.";
            return false;
        }

        if (ModelLoadTimeoutSeconds is < 1 or > 900)
        {
            error = "LM Studio modelLoadTimeoutSeconds must be between 1 and 900.";
            return false;
        }

        if (RequestTimeoutSeconds is < 1 or > 1800)
        {
            error = "LM Studio requestTimeoutSeconds must be between 1 and 1800.";
            return false;
        }

        if (ModelIdentifier is not null && string.IsNullOrWhiteSpace(ModelIdentifier))
        {
            error = "LM Studio modelIdentifier must be omitted or contain a model identifier.";
            return false;
        }

        return true;
    }
}

public sealed record LatticeConfiguration
{
    public LmStudioConfiguration LmStudio { get; init; } = new();
}
