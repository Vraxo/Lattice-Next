using System.Globalization;

namespace Lattice.Translators.LmStudio;

public sealed record LmStudioOptions
{
    public LmStudioOptions(
        Uri baseAddress,
        string model,
        bool autoStartServer = false,
        bool autoLoadModel = false,
        TimeSpan? requestTimeout = null,
        string cliPath = "lms",
        string? apiToken = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri ||
            baseAddress.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(baseAddress.Query) ||
            !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            throw new ArgumentException(
                "The LM Studio base address must be an absolute HTTP or HTTPS URL without a query or fragment.",
                nameof(baseAddress));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("A model identifier is required.", nameof(model));
        }

        var timeout = requestTimeout ?? TimeSpan.FromSeconds(120);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The request timeout must be greater than zero.");
        }

        BaseAddress = new Uri(baseAddress.AbsoluteUri.TrimEnd('/') + "/");
        Model = model.Trim();
        AutoStartServer = autoStartServer;
        AutoLoadModel = autoLoadModel;
        RequestTimeout = timeout;
        CliPath = string.IsNullOrWhiteSpace(cliPath) ? "lms" : cliPath.Trim();
        ApiToken = string.IsNullOrWhiteSpace(apiToken) ? null : apiToken.Trim();
    }

    public Uri BaseAddress { get; }

    public string Model { get; }

    public bool AutoStartServer { get; }

    public bool AutoLoadModel { get; }

    public TimeSpan RequestTimeout { get; }

    public string CliPath { get; }

    public string? ApiToken { get; }

    public static bool TryCreateFromEnvironment(
        out LmStudioOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        var model = Environment.GetEnvironmentVariable("LATTICE_LMSTUDIO_MODEL");

        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var baseAddressText = Environment.GetEnvironmentVariable("LATTICE_LMSTUDIO_URL");

        if (string.IsNullOrWhiteSpace(baseAddressText))
        {
            baseAddressText = "http://127.0.0.1:1234/";
        }

        if (!Uri.TryCreate(baseAddressText, UriKind.Absolute, out var baseAddress))
        {
            error = "LATTICE_LMSTUDIO_URL must be an absolute HTTP or HTTPS URL.";
            return false;
        }

        if (!TryReadBoolean("LATTICE_LMSTUDIO_AUTO_START", out var autoStartServer, out error) ||
            !TryReadBoolean("LATTICE_LMSTUDIO_AUTO_LOAD", out var autoLoadModel, out error))
        {
            return false;
        }

        var timeout = TimeSpan.FromSeconds(120);
        var timeoutText = Environment.GetEnvironmentVariable("LATTICE_LMSTUDIO_TIMEOUT_SECONDS");

        if (!string.IsNullOrWhiteSpace(timeoutText))
        {
            if (!int.TryParse(
                    timeoutText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var timeoutSeconds) ||
                timeoutSeconds is < 1 or > 3600)
            {
                error = "LATTICE_LMSTUDIO_TIMEOUT_SECONDS must be an integer from 1 to 3600.";
                return false;
            }

            timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        options = new LmStudioOptions(
            baseAddress,
            model,
            autoStartServer,
            autoLoadModel,
            timeout,
            Environment.GetEnvironmentVariable("LATTICE_LMSTUDIO_CLI") ?? "lms",
            Environment.GetEnvironmentVariable("LATTICE_LMSTUDIO_API_TOKEN"));

        return true;
    }

    private static bool TryReadBoolean(
        string name,
        out bool value,
        out string? error)
    {
        var text = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(text))
        {
            value = false;
            error = null;
            return true;
        }

        if (bool.TryParse(text, out value))
        {
            error = null;
            return true;
        }

        error = $"{name} must be true or false.";
        return false;
    }
}
