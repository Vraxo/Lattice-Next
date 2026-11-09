using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Lattice.Core.Tools;

namespace Lattice.Tooling.Stdio;

public sealed class StdioTool : ITool
{
    private const int ProtocolVersion = 1;
    private const int MaximumResponseCharacters = 1024 * 1024;
    private const int MaximumDiagnosticCharacters = 8192;

    private readonly string command;
    private readonly IReadOnlyList<string> arguments;
    private readonly string workingDirectory;

    public StdioTool(
        ToolDescriptor descriptor,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(arguments);

        Descriptor = descriptor;
        this.command = command;
        this.arguments = arguments;
        this.workingDirectory = workingDirectory;
    }

    public ToolDescriptor Descriptor { get; }

    public async ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var request = JsonSerializer.Serialize(new
        {
            protocolVersion = ProtocolVersion,
            requestId,
            operation = "execute",
            arguments
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in this.arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                return ToolExecutionResult.Failure("The external tool process could not be started.");
            }

            var standardErrorTask = process.StandardError.ReadToEndAsync();

            await process.StandardInput.WriteLineAsync(request);
            await process.StandardInput.FlushAsync(cancellationToken);
            process.StandardInput.Close();

            var responseLine = await process.StandardOutput.ReadLineAsync(cancellationToken);

            if (responseLine is null)
            {
                await process.WaitForExitAsync(cancellationToken);
                var emptyOutputError = await standardErrorTask;

                return ToolExecutionResult.Failure(
                    CreateProcessFailure(process.ExitCode, emptyOutputError));
            }

            if (responseLine.Length > MaximumResponseCharacters)
            {
                TryKill(process);
                return ToolExecutionResult.Failure("The external tool response exceeded the one-megabyte limit.");
            }

            var trailingOutputTask = process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var standardError = await standardErrorTask;
            var trailingOutput = await trailingOutputTask;

            if (process.ExitCode != 0)
            {
                return ToolExecutionResult.Failure(
                    CreateProcessFailure(process.ExitCode, standardError));
            }

            if (!string.IsNullOrWhiteSpace(trailingOutput))
            {
                return ToolExecutionResult.Failure(
                    "The external tool wrote unexpected data after its response.");
            }

            return ParseResponse(responseLine, requestId);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            Win32Exception or
            InvalidOperationException)
        {
            TryKill(process);
            return ToolExecutionResult.Failure("The external tool process failed during communication.");
        }
        finally
        {
            TryKill(process);
        }
    }

    private static ToolExecutionResult ParseResponse(
        string responseLine,
        string expectedRequestId)
    {
        try
        {
            using var document = JsonDocument.Parse(responseLine);
            var response = document.RootElement;

            if (response.ValueKind != JsonValueKind.Object ||
                !HasUniqueProperties(response))
            {
                return ToolExecutionResult.Failure("The external tool returned an invalid response object.");
            }

            if (!TryGetInteger(response, "protocolVersion", out var protocolVersion) ||
                protocolVersion != ProtocolVersion)
            {
                return ToolExecutionResult.Failure("The external tool response uses an unsupported protocol version.");
            }

            if (!TryGetString(response, "requestId", out var requestId) ||
                !string.Equals(requestId, expectedRequestId, StringComparison.Ordinal))
            {
                return ToolExecutionResult.Failure("The external tool response has an invalid request ID.");
            }

            if (!response.TryGetProperty("success", out var successValue) ||
                successValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return ToolExecutionResult.Failure("The external tool response is missing its success status.");
            }

            if (!successValue.GetBoolean())
            {
                return ToolExecutionResult.Failure(
                    TryGetString(response, "error", out var error)
                        ? error!
                        : "The external tool reported a failure.");
            }

            if (!response.TryGetProperty("result", out var result))
            {
                return ToolExecutionResult.Failure("The external tool response is missing its result.");
            }

            return ToolExecutionResult.Success(result.GetRawText());
        }
        catch (JsonException)
        {
            return ToolExecutionResult.Failure("The external tool returned invalid JSON.");
        }
    }

    private static string CreateProcessFailure(int exitCode, string standardError)
    {
        var diagnostic = standardError.Trim();

        if (diagnostic.Length > MaximumDiagnosticCharacters)
        {
            diagnostic = diagnostic[..MaximumDiagnosticCharacters];
        }

        return diagnostic.Length == 0
            ? $"The external tool exited with code {exitCode} without returning a response."
            : $"The external tool exited with code {exitCode}: {diagnostic}";
    }

    private static bool TryGetInteger(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;

        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasUniqueProperties(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }
}
