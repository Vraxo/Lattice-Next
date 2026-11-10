using System.Text.Json;

namespace Lattice.Core.Tools;

public sealed class ToolRegistry
{
    private const ToolPermission AllPermissions = ToolPermission.Read | ToolPermission.Write;

    private readonly Dictionary<string, ITool> tools;
    private readonly Dictionary<string, ToolPermission> toolPermissions;
    private readonly TimeSpan executionTimeout;
    private readonly ToolPermission grantedPermissions;

    public ToolRegistry(
        IEnumerable<ITool> registeredTools,
        ToolPermission grantedPermissions = ToolPermission.None,
        TimeSpan? executionTimeout = null,
        IReadOnlyDictionary<string, ToolPermission>? toolPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(registeredTools);

        if ((grantedPermissions & ~AllPermissions) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grantedPermissions));
        }

        this.grantedPermissions = grantedPermissions;
        this.toolPermissions = new Dictionary<string, ToolPermission>(
            StringComparer.OrdinalIgnoreCase);

        if (toolPermissions is not null)
        {
            foreach (var pair in toolPermissions)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    (pair.Value & ~AllPermissions) != 0)
                {
                    throw new ArgumentException(
                        "Tool permission grants must use a non-empty tool ID and known permissions.",
                        nameof(toolPermissions));
                }

                this.toolPermissions.Add(pair.Key, pair.Value);
            }
        }

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

        var toolGrantedPermissions = toolPermissions.TryGetValue(
            tool.Descriptor.Id,
            out var configuredToolPermissions)
            ? configuredToolPermissions
            : ToolPermission.None;
        var effectivePermissions = grantedPermissions | toolGrantedPermissions;

        if ((tool.Descriptor.RequiredPermissions & effectivePermissions) != tool.Descriptor.RequiredPermissions)
        {
            return ToolInvocationResult.Failure(
                $"Tool '{tool.Descriptor.Id}' requires permissions that have not been granted.");
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
                    timeout.Token).AsTask().WaitAsync(timeout.Token);

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
            descriptor.Inputs.IsDefault ||
            (descriptor.RequiredPermissions & ~AllPermissions) != 0)
        {
            throw new ArgumentException("A tool descriptor must define its ID, name, description, and inputs.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in descriptor.Inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Name) ||
                string.IsNullOrWhiteSpace(input.Description) ||
                !Enum.IsDefined(input.Type) ||
                !names.Add(input.Name))
            {
                throw new ArgumentException(
                    $"Tool '{descriptor.Id}' has an invalid or duplicate input descriptor.");
            }
        }
    }
}
