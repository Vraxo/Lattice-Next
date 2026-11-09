using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Tooling.Stdio;

public static class StdioToolProvider
{
    private const string ManifestFileName = "tool.json";
    private const long MaximumManifestBytes = 1024 * 1024;

    public static bool TryDiscover(
        string toolsDirectory,
        out IReadOnlyList<ITool> tools,
        out string? error)
    {
        tools = Array.Empty<ITool>();
        error = null;

        if (string.IsNullOrWhiteSpace(toolsDirectory))
        {
            error = "The tools directory must be specified.";
            return false;
        }

        try
        {
            var rootDirectory = Path.GetFullPath(toolsDirectory);

            if (!Directory.Exists(rootDirectory))
            {
                return true;
            }

            var discovered = new List<ITool>();
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var toolDirectory in Directory
                .EnumerateDirectories(rootDirectory)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                var manifestPath = Path.Combine(toolDirectory, ManifestFileName);

                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                if (!TryLoadTool(manifestPath, toolDirectory, out var tool, out error))
                {
                    return false;
                }

                if (!identifiers.Add(tool!.Descriptor.Id))
                {
                    error = $"More than one external tool uses ID '{tool.Descriptor.Id}'.";
                    return false;
                }

                discovered.Add(tool);
            }

            tools = discovered;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            error = "The configured tools directory could not be read.";
            return false;
        }
    }

    private static bool TryLoadTool(
        string manifestPath,
        string toolDirectory,
        out ITool? tool,
        out string? error)
    {
        tool = null;
        error = null;

        try
        {
            var file = new FileInfo(manifestPath);

            if (file.Length > MaximumManifestBytes)
            {
                error = $"Tool manifest '{manifestPath}' exceeds the one-megabyte limit.";
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(
                    root,
                    "protocolVersion",
                    "id",
                    "name",
                    "description",
                    "command",
                    "arguments",
                    "inputs",
                    "permissions",
                    "intents"))
            {
                error = $"Tool manifest '{manifestPath}' has an invalid root object or unknown properties.";
                return false;
            }

            if (!TryGetInteger(root, "protocolVersion", out var protocolVersion) ||
                protocolVersion != 1)
            {
                error = $"Tool manifest '{manifestPath}' uses an unsupported protocol version.";
                return false;
            }

            if (!TryGetString(root, "id", out var id) ||
                !TryGetString(root, "name", out var name) ||
                !TryGetString(root, "description", out var description) ||
                !TryGetString(root, "command", out var command))
            {
                error = $"Tool manifest '{manifestPath}' must define id, name, description, and command.";
                return false;
            }

            if (!TryGetArguments(root, out var arguments) ||
                !TryGetInputs(root, out var inputs) ||
                !TryGetPermissions(root, out var permissions) ||
                !TryGetIntents(root, inputs, out var intents))
            {
                error = $"Tool manifest '{manifestPath}' contains an invalid arguments, inputs, or permissions value.";
                return false;
            }

            if (!TryResolveCommand(command!, toolDirectory, out var resolvedCommand))
            {
                error = $"Tool manifest '{manifestPath}' points to an executable outside its tool directory.";
                return false;
            }

            var descriptor = new ToolDescriptor(
                id!,
                name!,
                description!,
                inputs,
                permissions,
                intents);

            if (string.IsNullOrWhiteSpace(descriptor.Id) ||
                string.IsNullOrWhiteSpace(descriptor.Name) ||
                string.IsNullOrWhiteSpace(descriptor.Description))
            {
                error = $"Tool manifest '{manifestPath}' contains an empty required value.";
                return false;
            }

            tool = new StdioTool(
                descriptor,
                resolvedCommand!,
                arguments,
                Path.GetFullPath(toolDirectory));

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            ArgumentException or
            NotSupportedException)
        {
            error = $"Tool manifest '{manifestPath}' could not be read or parsed.";
            return false;
        }
    }

    private static bool TryGetArguments(
        JsonElement root,
        out IReadOnlyList<string> arguments)
    {
        arguments = Array.Empty<string>();

        if (!root.TryGetProperty("arguments", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<string>();

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            parsed.Add(item.GetString()!);
        }

        arguments = parsed;
        return true;
    }

    private static bool TryGetInputs(
        JsonElement root,
        out ImmutableArray<ToolInputDescriptor> inputs)
    {
        inputs = ImmutableArray<ToolInputDescriptor>.Empty;

        if (!root.TryGetProperty("inputs", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = ImmutableArray.CreateBuilder<ToolInputDescriptor>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "name", "description", "type", "required") ||
                !TryGetString(item, "name", out var name) ||
                !TryGetString(item, "description", out var description) ||
                !TryGetString(item, "type", out var type) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(description) ||
                !names.Add(name!))
            {
                return false;
            }

            var inputType = type switch
            {
                "string" => ToolInputType.String,
                "number" => ToolInputType.Number,
                "boolean" => ToolInputType.Boolean,
                _ => (ToolInputType?)null
            };

            if (inputType is null)
            {
                return false;
            }

            var required = true;

            if (item.TryGetProperty("required", out var requiredValue))
            {
                if (requiredValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return false;
                }

                required = requiredValue.GetBoolean();
            }

            parsed.Add(new ToolInputDescriptor(
                name!,
                description!,
                inputType.Value,
                required));
        }

        inputs = parsed.ToImmutable();
        return true;
    }

    private static bool TryGetIntents(
        JsonElement root,
        ImmutableArray<ToolInputDescriptor> inputs,
        out ImmutableArray<ToolIntentDefinition> intents)
    {
        intents = ImmutableArray<ToolIntentDefinition>.Empty;

        if (!root.TryGetProperty("intents", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var inputNames = inputs
            .Select(input => input.Name)
            .ToHashSet(StringComparer.Ordinal);
        var requiredInputNames = inputs
            .Where(input => input.Required)
            .Select(input => input.Name)
            .ToHashSet(StringComparer.Ordinal);
        var parsed = ImmutableArray.CreateBuilder<ToolIntentDefinition>();

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "pattern", "arguments", "response") ||
                !TryGetString(item, "pattern", out var pattern) ||
                !TryGetString(item, "response", out var response) ||
                !item.TryGetProperty("arguments", out var argumentValues) ||
                argumentValues.ValueKind != JsonValueKind.Object ||
                !TryGetPatternSlots(pattern!, out var slots))
            {
                return false;
            }

            var argumentBindings = ImmutableDictionary.CreateBuilder<string, ToolIntentBinding>(StringComparer.Ordinal);

            foreach (var argument in argumentValues.EnumerateObject())
            {
                if (!inputNames.Contains(argument.Name) ||
                    argumentBindings.ContainsKey(argument.Name))
                {
                    return false;
                }

                if (argument.Value.ValueKind == JsonValueKind.String)
                {
                    var text = argument.Value.GetString();

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return false;
                    }

                    if (text.StartsWith('
    {
        permissions = ToolPermission.None;

        if (!root.TryGetProperty("permissions", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            permissions |= item.GetString() switch
            {
                "read" => ToolPermission.Read,
                "write" => ToolPermission.Write,
                _ => (ToolPermission)(-1)
            };

            if ((permissions & ~(ToolPermission.Read | ToolPermission.Write)) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveCommand(
        string command,
        string toolDirectory,
        out string? resolvedCommand)
    {
        resolvedCommand = null;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (Path.IsPathRooted(command))
        {
            resolvedCommand = command;
            return true;
        }

        if (!command.Contains(Path.DirectorySeparatorChar) &&
            !command.Contains(Path.AltDirectorySeparatorChar))
        {
            var localCommandPath = Path.Combine(toolDirectory, command);

            resolvedCommand = File.Exists(localCommandPath)
                ? Path.GetFullPath(localCommandPath)
                : command;
            return true;
        }

        var fullToolDirectory = Path.GetFullPath(toolDirectory);
        var fullCommandPath = Path.GetFullPath(command, fullToolDirectory);
        var relativePath = Path.GetRelativePath(fullToolDirectory, fullCommandPath);

        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            return false;
        }

        resolvedCommand = fullCommandPath;
        return true;
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

    private static bool HasOnlyProperties(
        JsonElement element,
        params string[] allowedProperties)
    {
        var allowed = new HashSet<string>(allowedProperties, StringComparer.Ordinal);
        var supplied = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !supplied.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }
}
))
                    {
                        var reference = text[1..];

                        if (reference != "previous" && !slots.Contains(reference))
                        {
                            return false;
                        }

                        argumentBindings.Add(
                            argument.Name,
                            new ToolIntentBinding(reference, null));
                        continue;
                    }
                }

                if (argument.Value.ValueKind is not (
                    JsonValueKind.String or
                    JsonValueKind.Number or
                    JsonValueKind.True or
                    JsonValueKind.False))
                {
                    return false;
                }

                argumentBindings.Add(
                    argument.Name,
                    new ToolIntentBinding(null, argument.Value.Clone()));
            }

            if (!requiredInputNames.IsSubsetOf(argumentBindings.Keys))
            {
                return false;
            }

            if (!TryValidateResponseTemplate(response!, argumentBindings.Keys))
            {
                return false;
            }

            parsed.Add(new ToolIntentDefinition(
                pattern!,
                argumentBindings.ToImmutable(),
                response!));
        }

        intents = parsed.ToImmutable();
        return true;
    }

    private static bool TryGetPatternSlots(
        string pattern,
        out HashSet<string> slots)
    {
        slots = new HashSet<string>(StringComparer.Ordinal);
        var validSlot = new Regex(
            @"^\{(?<name>[A-Za-z][A-Za-z0-9_]*):(?<type>number|word|text)\}$",
            RegexOptions.CultureInvariant);

        foreach (var rawToken in pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.TrimEnd('.', '?', '!', ',', ';', ':');

            if (token.StartsWith('{') || token.EndsWith('}'))
            {
                var match = validSlot.Match(token);

                if (!match.Success || !slots.Add(match.Groups["name"].Value))
                {
                    return false;
                }
            }
            else if (token.Contains('{') || token.Contains('}'))
            {
                return false;
            }
        }

        return slots.Count > 0;
    }

    private static bool TryValidateResponseTemplate(
        string response,
        IEnumerable<string> argumentNames)
    {
        var available = argumentNames
            .Append("result")
            .ToHashSet(StringComparer.Ordinal);
        var matches = Regex.Matches(
            response,
            @"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}",
            RegexOptions.CultureInvariant);

        var remaining = Regex.Replace(
            response,
            @"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}",
            string.Empty,
            RegexOptions.CultureInvariant);

        if (remaining.Contains('{') || remaining.Contains('}'))
        {
            return false;
        }

        return matches.Count > 0 &&
            matches.Cast<Match>().All(match => available.Contains(match.Groups["name"].Value)) &&
            matches.Cast<Match>().Any(match => match.Groups["name"].Value == "result");
    }

    private static bool TryGetPermissions(
        JsonElement root,
        out ToolPermission permissions)
    {
        permissions = ToolPermission.None;

        if (!root.TryGetProperty("permissions", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            permissions |= item.GetString() switch
            {
                "read" => ToolPermission.Read,
                "write" => ToolPermission.Write,
                _ => (ToolPermission)(-1)
            };

            if ((permissions & ~(ToolPermission.Read | ToolPermission.Write)) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveCommand(
        string command,
        string toolDirectory,
        out string? resolvedCommand)
    {
        resolvedCommand = null;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (Path.IsPathRooted(command))
        {
            resolvedCommand = command;
            return true;
        }

        if (!command.Contains(Path.DirectorySeparatorChar) &&
            !command.Contains(Path.AltDirectorySeparatorChar))
        {
            var localCommandPath = Path.Combine(toolDirectory, command);

            resolvedCommand = File.Exists(localCommandPath)
                ? Path.GetFullPath(localCommandPath)
                : command;
            return true;
        }

        var fullToolDirectory = Path.GetFullPath(toolDirectory);
        var fullCommandPath = Path.GetFullPath(command, fullToolDirectory);
        var relativePath = Path.GetRelativePath(fullToolDirectory, fullCommandPath);

        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            return false;
        }

        resolvedCommand = fullCommandPath;
        return true;
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

    private static bool HasOnlyProperties(
        JsonElement element,
        params string[] allowedProperties)
    {
        var allowed = new HashSet<string>(allowedProperties, StringComparer.Ordinal);
        var supplied = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !supplied.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }
}
