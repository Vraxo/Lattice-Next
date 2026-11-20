using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonCollectionProgramParser
{
    private static readonly Regex IntegerConversionFunctionPattern = new(
        @"\bfunction\s+(?:named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+)?(?:that\s+)?(?:accepts?\s+(?<parameter>[A-Za-z_][A-Za-z0-9_]*)\s+and\s+)?converts?\s+(?:the\s+)?(?<input>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+an?\s+integer\s+and\s+returns?\s+(?:a\s+)?(?:clear\s+)?(?:result|message)\s+(?:when|if)\s+(?:the\s+)?(?:text|input)\s+is\s+not\s+a\s+valid\s+integer",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CollectionMappingFunctionPattern = new(
        @"\b(?:given|from)\s+(?:a|the)\s+list\s+of\s+(?<kind>names?|words?|numbers?|values?|items?)\s*,?\s*(?:creates?|builds?|makes?)\s+(?:a\s+)?dictionary(?:\s+named\s+(?<dictionary>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+maps?\s+each\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+(?<mapping>.+?)\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CollectionFilterFunctionPattern = new(
        @"\bfunction\s+(?:named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+)?(?:that\s+)?(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+numbers?(?:\s+(?:named\s+)?(?<parameter>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+returns?\s+(?:a\s+)?new\s+list\s+containing\s+only\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)(?:\s+values?)?\s+(?<comparison>greater\s+than\s+or\s+equal\s+to|at\s+least|less\s+than\s+or\s+equal\s+to|at\s+most|not\s+equal\s+to|greater\s+than|more\s+than|less\s+than|below|equal\s+to|equals)\s+(?<threshold>(?:the\s+)?average(?:\s+of\s+(?:the\s+)?(?<averageParameter>[A-Za-z_][A-Za-z0-9_]*))?|\d+(?:\.\d+)?(?:\s*[+*/%\-]\s*\d+(?:\.\d+)?)*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SortRecordsByFieldsPattern = new(
        @"\b(?:creates?|defines?|writes?)\s+(?:a\s+)?(?:reusable\s+)?function(?:\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+records(?:\s+(?:named\s+)?(?<records>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+(?:a\s+)?list\s+of\s+field\s+names(?:\s+(?:named\s+)?(?<fields>[A-Za-z_][A-Za-z0-9_]*))?\s*,?\s+then\s+returns?\s+(?:the\s+)?records?\s+sorted\s+(?:case[- ]insensitively\s+)?by\s+(?:those|the)\s+fields(?:\s+case[- ]insensitively)?(?:\s+in\s+descending\s+order)?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SortRecordsByFieldPattern = new(
        @"\b(?:creates?|defines?|writes?)\s+(?:a\s+)?(?:reusable\s+)?function(?:\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+records(?:\s+(?:named\s+)?(?<records>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+(?:a\s+)?field\s+name(?:\s+(?:named\s+)?(?<field>[A-Za-z_][A-Za-z0-9_]*))?\s*,?\s+then\s+returns?\s+(?:the\s+)?records?\s+sorted\s+(?:case[- ]insensitively\s+)?by\s+(?:that|the)\s+field(?:\s+case[- ]insensitively)?(?:\s+in\s+(?:descending|reverse)\s+order)?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex JsonCollectionRoundTripPattern = new(
        @"\b(?:saves?|writes?)\s+(?:a|the)\s+list\s+of\s+dictionaries(?:\s+(?:named|called)\s+(?<records>[A-Za-z_][A-Za-z0-9_]*))?\s+to\s+(?:a|the)\s+JSON\s+file(?:\s+(?:named|called)\s+(?<path>[A-Za-z0-9_.-]+\.json))?\s+and\s+then\s+loads?\s+it\s+back\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DictionaryIterationPattern = new(
        @"\bstores?\s+(?<items>\{[^}]*\})\s+(?:in|as)\s+(?:a\s+)?dictionary\s+(?:named\s+)?(?<dictionary>[A-Za-z_][A-Za-z0-9_]*)\s*,?\s+then\s+(?:prints?|displays?|outputs?)\s+each\s+(?<key>[A-Za-z_][A-Za-z0-9_]*)\s+and\s+(?:its\s+)?(?<value>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static readonly Regex CollectionGroupingAggregationPattern = new(
        @"\b(?:groups?|organizes?)\s+(?:a|the)\s+list\s+of\s+(?<record>[A-Za-z_][A-Za-z0-9_]*)\s+records?\s+by\s+(?<group>[A-Za-z_][A-Za-z0-9_]*)\s+and\s+(?:calculates?|computes?|sums?)\s+(?:each\s+(?<owner>[A-Za-z_][A-Za-z0-9_]*)'?s\s+)?(?:total\s+)?(?<measure>spending|amount|cost|price|value)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetCollectionMappingFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionMappingFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var kind = match.Groups["kind"].Value;
        var item = match.Groups["item"].Value;
        var parameter = kind.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? kind
            : kind + "s";
        var expressionText = match.Groups["mapping"].Value.Trim().TrimEnd('.', '?', '!');
        var expression = expressionText.ToLowerInvariant() switch
        {
            "the number of characters in it" or "the length of it" or "its length" =>
                $"len({item})",
            "the square of it" or "its square" =>
                $"{item} * {item}",
            _ => expressionText
        };

        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            !PythonExpressionParser.IsArithmeticExpression(expression) ||
            Regex.Matches(expression, @"[A-Za-z_][A-Za-z0-9_]*")
                .Any(identifier => !identifier.Value.Equals(item, StringComparison.OrdinalIgnoreCase) &&
                    !identifier.Value.Equals("len", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var function = "map_values";
        var sample = kind.StartsWith("name", StringComparison.OrdinalIgnoreCase) ||
            kind.StartsWith("word", StringComparison.OrdinalIgnoreCase)
            ? @"[""Ada"", ""Grace"", ""Linus""]"
            : "[1, 2, 3]";

        var dictionary = match.Groups["dictionary"].Success
            ? match.Groups["dictionary"].Value
            : "mapped_values";
        var dictionaryExpression = $"{{{item}: {expression} for {item} in {parameter}}}";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{function}({parameter})"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{dictionary} = {dictionaryExpression}"),
            new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                dictionary),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                $"{function}({sample})"));
        return true;
    }

    internal static bool TryGetCollectionFilterFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionFilterFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var threshold = match.Groups["threshold"].Value;
        var isAverage = threshold.Contains("average", StringComparison.OrdinalIgnoreCase);
        var name = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : isAverage ? "above_average" : "filter_values";
        var parameter = match.Groups["parameter"].Success ? match.Groups["parameter"].Value : "values";
        var item = match.Groups["item"].Value;
        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) &&
            item.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            item = item[..^1];
        }

        var averageParameter = match.Groups["averageParameter"].Success
            ? match.Groups["averageParameter"].Value
            : parameter;

        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            (isAverage && !averageParameter.Equals(parameter, StringComparison.OrdinalIgnoreCase)) ||
            name.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(item, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var comparisonText = Regex.Replace(
            match.Groups["comparison"].Value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim().ToLowerInvariant();
        var comparison = comparisonText switch
        {
            "greater than" or "more than" => ">",
            "greater than or equal to" or "at least" => ">=",
            "less than" or "below" => "<",
            "less than or equal to" or "at most" => "<=",
            "equal to" or "equals" => "==",
            "not equal to" => "!=",
            _ => string.Empty
        };
        var thresholdExpression = isAverage
            ? $"sum({parameter}) / len({parameter})"
            : threshold;
        if (comparison.Length == 0 || (!isAverage && !PythonExpressionParser.IsArithmeticExpression(thresholdExpression)))
        {
            return false;
        }
        var expression = $"[{item} for {item} in {parameter} if {item} {comparison} {thresholdExpression}]";
        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({parameter})"),
            new TaskRequestStep("return-value-indented-expression", "expression", expression),
            new TaskRequestStep("write-standard-output", "expression", $"{name}([1, 2, 3, 4, 5])"));
        return true;
    }

    internal static bool TryGetIntegerConversionFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = IntegerConversionFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var parameter = match.Groups["parameter"].Success
            ? match.Groups["parameter"].Value
            : match.Groups["input"].Value;
        var name = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : "convert_to_integer";

        if (name.Equals(parameter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({parameter})"),
            new TaskRequestStep("begin-try-indented", "statement", "try"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", $"int({parameter})"),
            new TaskRequestStep("begin-except-indented", "exception-type", "ValueError"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", "\"Invalid integer\""),
            new TaskRequestStep("write-standard-output", "expression", $"{name}(\"42\")"),
            new TaskRequestStep("write-standard-output", "expression", $"{name}(\"not-a-number\")"));
        return true;
    }

    internal static bool TryGetDictionaryIterationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = DictionaryIterationPattern.Match(source);
        if (!match.Success ||
            !TryNormalizeSimpleDictionaryLiteral(match.Groups["items"].Value, out var items))
        {
            return false;
        }

        var dictionary = match.Groups["dictionary"].Value;
        var key = match.Groups["key"].Value;
        var value = match.Groups["value"].Value;
        if (dictionary.Equals(key, StringComparison.OrdinalIgnoreCase) ||
            dictionary.Equals(value, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", $"{dictionary} = {items}"),
            new TaskRequestStep(
                "begin-loop",
                "loop-header",
                $"{key}, {value} in {dictionary}.items()"),
            new TaskRequestStep(
                "write-standard-output-indented-expression",
                "expression",
                $"{key}, {value}"));
        return true;
    }

    internal static bool TryGetSortRecordsByFieldsProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = SortRecordsByFieldsPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var functionName = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : "sort_records";
        var recordsName = match.Groups["records"].Success
            ? match.Groups["records"].Value
            : "records";
        var fieldsName = match.Groups["fields"].Success
            ? match.Groups["fields"].Value
            : "fields";
        var caseInsensitive = source.Contains("case-insensitively", StringComparison.OrdinalIgnoreCase)
            || source.Contains("case insensitively", StringComparison.OrdinalIgnoreCase);
        var descending = source.Contains("descending order", StringComparison.OrdinalIgnoreCase);
        var sampleRecords = """[{"department": "Engineering", "name": "Ada"}, {"department": "Design", "name": "Grace"}, {"department": "Engineering", "name": "Linus"}]""";
        var sortKey = caseInsensitive
            ? $"tuple(record[field].casefold() for field in {fieldsName})"
            : $"tuple(record[field] for field in {fieldsName})";

        steps = ImmutableArray.Create(
            new TaskRequestStep(
                "begin-function",
                "function-header",
                $"{functionName}({recordsName}, {fieldsName})"),
            new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                $"sorted({recordsName}, key=lambda record: {sortKey}{(descending ? ", reverse=True" : string.Empty)})"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                $"{functionName}({sampleRecords}, [\"department\", \"name\"])"));
        return true;
    }

    internal static bool TryGetSortRecordsByFieldProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = SortRecordsByFieldPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var functionName = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : "sort_records";
        var recordsName = match.Groups["records"].Success
            ? match.Groups["records"].Value
            : "records";
        var fieldName = match.Groups["field"].Success
            ? match.Groups["field"].Value
            : "field";
        var caseInsensitive = source.Contains("case-insensitively", StringComparison.OrdinalIgnoreCase)
            || source.Contains("case insensitively", StringComparison.OrdinalIgnoreCase);
        var sampleRecords = caseInsensitive
            ? "[{'name': 'ada', 'age': 36}, {'name': 'BOB', 'age': 42}, {'name': 'Grace', 'age': 85}]"
            : "[{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}, {\"name\": \"Linus\", \"age\": 55}]";
        var sampleField = caseInsensitive ? "'name'" : "\"age\"";

        steps = ImmutableArray.Create(
            new TaskRequestStep(
                "begin-function",
                "function-header",
                $"{functionName}({recordsName}, {fieldName})"),
            new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                $"sorted({recordsName}, key=lambda record: record[{fieldName}]{(source.Contains("case-insensitively", StringComparison.OrdinalIgnoreCase) || source.Contains("case insensitively", StringComparison.OrdinalIgnoreCase) ? ".casefold()" : string.Empty)}{(source.Contains("descending order", StringComparison.OrdinalIgnoreCase) || source.Contains("reverse order", StringComparison.OrdinalIgnoreCase) ? ", reverse=True" : string.Empty)})"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                $"{functionName}({sampleRecords}, {sampleField})"));
        return true;
    }

    internal static bool TryGetJsonCollectionRoundTripProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = JsonCollectionRoundTripPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var recordsName = match.Groups["records"].Success
            ? match.Groups["records"].Value
            : "records";
        var filePath = match.Groups["path"].Success
            ? match.Groups["path"].Value
            : "records.json";
        var loadedName = $"loaded_{recordsName}";

        steps = ImmutableArray.Create(
            new TaskRequestStep("import-module", "module-name", "json"),
            new TaskRequestStep(
                "assign-variable",
                "assignment",
                $"{recordsName} = [{{\"name\": \"Ada\", \"age\": 36}}, {{\"name\": \"Grace\", \"age\": 85}}]"),
            new TaskRequestStep(
                "begin-with",
                "expression",
                $"open(\"{filePath}\", \"w\", encoding=\"utf-8\") as output_file"),
            new TaskRequestStep(
                "write-expression-indented",
                "expression",
                $"{recordsName}, output_file, indent=2"),
            new TaskRequestStep(
                "begin-with",
                "expression",
                $"open(\"{filePath}\", \"r\", encoding=\"utf-8\") as input_file"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{loadedName} = json.load(input_file)"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                loadedName));
        return true;
    }


    internal static bool TryGetCollectionGroupingAggregationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionGroupingAggregationPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var record = match.Groups["record"].Value;
        var group = match.Groups["group"].Value;
        var owner = match.Groups["owner"].Success ? match.Groups["owner"].Value : group;
        var measure = match.Groups["measure"].Value.Equals("spending", StringComparison.OrdinalIgnoreCase)
            ? "amount"
            : match.Groups["measure"].Value;

        if (!owner.Equals(group, StringComparison.OrdinalIgnoreCase) ||
            record.Equals(group, StringComparison.OrdinalIgnoreCase) ||
            record.Equals("for", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var records = $"records = [{{\"{group}\": \"Ada\", \"{measure}\": 25.0}}, {{\"{group}\": \"Grace\", \"{measure}\": 40.0}}, {{\"{group}\": \"Ada\", \"{measure}\": 15.0}}]";
        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", records),
            new TaskRequestStep("assign-variable", "assignment", "totals = {}"),
            new TaskRequestStep("begin-loop", "loop-header", $"{record} in records"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{group} = {record}[\"{group}\"]"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"totals[{group}] = totals.get({group}, 0) + {record}[\"{measure}\"]"),
            new TaskRequestStep("begin-loop", "loop-header", $"{group}, total in totals.items()"),
            new TaskRequestStep("write-standard-output-indented-expression", "expression", $"{group}, total"));
        return true;
    }

    private static bool TryNormalizeSimpleDictionaryLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string key = @"(?:'[^'\\]*'|""[^""\\]*"")";
        const string number = @"[+-]?\d+(?:\.\d+)?";
        if (!Regex.IsMatch(
            normalized,
            $@"^\{{\s*{key}\s*:\s*{number}(?:\s*,\s*{key}\s*:\s*{number})*\s*\}}$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*:\s*", ": ");
        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

}
