using System.Text.Json;

namespace Lattice.Core.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> tools;
    private readonly TimeSpan executionTimeout;

    public ToolRegistry(
        IEnumerable<ITool> registeredTools,
        TimeSpan? executionTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(registeredTools);

        this.executionTimeout = executionTimeout ?? TimeSpan.FromSeconds(5);

        if (this.executionTimeout <= TimeSpan.Zero ||
            this.executionTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionTimeout),
                "Tool execution timeout must be greater than zero and no more than one minute.");
        }

        tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in registeredTools)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ValidateDescriptor(tool.Descriptor);

            if (!tools.TryAdd(tool.Descriptor.Id, tool))
            {
                throw new ArgumentException(
                    $"A tool with ID '{tool.Descriptor.Id}' is already registered.",
                    nameof(registeredTools));
            }
        }
    }

    public IReadOnlyCollection<ToolDescriptor> Descriptors =>
        tools.Values.Select(tool => tool.Descriptor).ToArray();

    public async ValueTask<ToolInvocationResult> TryInvokeAsync(
        string? toolId,
        string? argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId) ||
            !tools.TryGetValue(toolId, out var tool))
        {
            return ToolInvocationResult.Failure(
                $"No tool is registered with ID '{toolId}'.");
        }

        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return ToolInvocationResult.Failure("Tool arguments must be a JSON object.");
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(argumentsJson);
        }
        catch (JsonException)
        {
            return ToolInvocationResult.Failure("Tool arguments are not valid JSON.");
        }

        using (document)
        {
            var validationError = ValidateArguments(tool.Descriptor, document.RootElement);

            if (validationError is not null)
            {
                return ToolInvocationResult.Failure(validationError);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(executionTimeout);

            try
            {
                var result = await tool.ExecuteAsync(
                    document.RootElement,
                    timeout.Token);

                if (!result.IsSuccess)
                {
                    return ToolInvocationResult.Failure(
                        result.Error ?? "The tool could not complete the request.");
                }

                if (result.Output is null)
                {
                    return ToolInvocationResult.Failure(
                        "The tool returned a successful result without output.");
                }

                return ToolInvocationResult.Success(
                    new ToolObservation(tool.Descriptor.Id, result.Output));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ToolInvocationResult.Failure("Tool invocation was cancelled.");
            }
            catch (OperationCanceledException)
            {
                return ToolInvocationResult.Failure("Tool execution exceeded its time limit.");
            }
            catch (Exception)
            {
                return ToolInvocationResult.Failure("The tool failed unexpectedly.");
            }
        }
    }

    private static string? ValidateArguments(
        ToolDescriptor descriptor,
        JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return "Tool arguments must be a JSON object.";
        }

        var suppliedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in arguments.EnumerateObject())
        {
            if (!suppliedNames.Add(property.Name))
            {
                return $"Tool argument '{property.Name}' was supplied more than once.";
            }

            var input = descriptor.Inputs.FirstOrDefault(
                candidate => string.Equals(candidate.Name, property.Name, StringComparison.Ordinal));

            if (input is null)
            {
                return $"Tool '{descriptor.Id}' does not accept an argument named '{property.Name}'.";
            }

            if (!MatchesType(property.Value, input.Type))
            {
                return $"Tool argument '{input.Name}' must be a {input.Type.ToString().ToLowerInvariant()}.";
            }
        }

        foreach (var input in descriptor.Inputs)
        {
            if (input.Required && !suppliedNames.Contains(input.Name))
            {
                return $"Required tool argument '{input.Name}' is missing.";
            }
        }

        return null;
    }

    private static bool MatchesType(JsonElement value, ToolInputType type) =>
        type switch
        {
            ToolInputType.String => value.ValueKind == JsonValueKind.String,
            ToolInputType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            ToolInputType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };

    private static void ValidateDescriptor(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.Id) ||
            string.IsNullOrWhiteSpace(descriptor.Name) ||
            string.IsNullOrWhiteSpace(descriptor.Description) ||
            descriptor.Inputs.IsDefault)
        {
            throw new ArgumentException("A tool descriptor must define its ID, name, description, and inputs.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in descriptor.Inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Name) ||
                string.IsNullOrWhiteSpace(input.Description) ||
                !names.Add(input.Name))
            {
                throw new ArgumentException(
                    $"Tool '{descriptor.Id}' has an invalid or duplicate input descriptor.");
            }
        }
    }
}
