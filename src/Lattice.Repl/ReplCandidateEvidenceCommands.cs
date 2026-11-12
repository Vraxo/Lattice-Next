using System.Globalization;
using System.Text.Json;
using Lattice.Client;
using Lattice.Core.Context;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplCandidateEvidenceCommands(
    ILatticeSession session,
    ConversationContext conversationContext)
{
    private const string CommandName = ":review-price";
    private const string AttributeCommandName = ":review-attribute";

    public async ValueTask<bool> TryHandleAsync(string input)
    {
        if (IsCommand(input, AttributeCommandName))
        {
            return await TryHandleAttributeAsync(input);
        }

        if (!IsCommand(input, CommandName))
        {
            return false;
        }

        var arguments = GetArguments(input, CommandName);
        if (string.IsNullOrWhiteSpace(arguments))
        {
            WriteError($"Usage: {CommandName} {{\"candidate\":\"Aster 14\",\"amount\":900,\"currencySymbol\":\"$\",\"excerpt\":\"Aster 14 is listed at $900.\"}}");
            return true;
        }

        var observation = conversationContext.LatestToolObservation;
        if (observation is null ||
            !string.Equals(observation.ToolId, "web-page-fetch", StringComparison.OrdinalIgnoreCase))
        {
            WriteError("Fetch a public page with the web-page-fetch tool first. Only its latest observation can be reviewed.");
            return true;
        }

        if (!LatticeExternalPageObservationParser.TryParse(
                observation.Content,
                out var page,
                out var pageError))
        {
            WriteError(pageError ?? "The latest page observation could not be read.");
            return true;
        }

        if (!TryParseRequest(arguments, out var request, out var requestError))
        {
            WriteError(requestError ?? "The review request is invalid.");
            return true;
        }

        if (!LatticeReviewedCandidateMoneyEvidenceFactory.TryCreate(
                page!,
                request!.Value.Candidate,
                request.Value.Amount,
                request.Value.CurrencySymbol,
                request.Value.Excerpt,
                out var evidence,
                out var evidenceError))
        {
            WriteError(evidenceError ?? "The reviewed evidence could not be created.");
            return true;
        }

        AnsiConsole.MarkupLine("[bold yellow]Review this external price observation[/]");
        AnsiConsole.WriteLine($"Candidate: {evidence!.Candidate}");
        AnsiConsole.WriteLine($"Price: {evidence.CurrencySymbol}{evidence.Amount.ToString(CultureInfo.InvariantCulture)}");
        AnsiConsole.WriteLine($"Source: {evidence.SourceUrl}");
        AnsiConsole.WriteLine($"Retrieved: {evidence.RetrievedAt:O}");
        AnsiConsole.WriteLine(evidence.FreshUntil is { } freshUntil
            ? $"Fresh until: {freshUntil:O}"
            : "Freshness: the source supplied no cache expiry; currentness is unverified.");
        AnsiConsole.WriteLine($"Supporting excerpt: {evidence.SourceText}");
        AnsiConsole.WriteLine("Fetched text is untrusted. Check the page and excerpt before accepting this claim.");
        AnsiConsole.Markup("[yellow]Type accept to add this evidence, or anything else to cancel: [/]" );

        if (!string.Equals(Console.ReadLine()?.Trim(), "accept", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[dim]Review cancelled; no candidate evidence was changed.[/]");
            AnsiConsole.WriteLine();
            return true;
        }

        var result = await session.AddReviewedCandidateMoneyEvidenceAsync(evidence);
        if (!result.IsSuccess)
        {
            WriteError(result.Error ?? "The reviewed candidate evidence was rejected.");
            return true;
        }

        AnsiConsole.MarkupLine($"[bold green]Accepted[/] reviewed total-cost evidence for {Markup.Escape(result.Candidate ?? evidence.Candidate)}.");
        AnsiConsole.MarkupLine("[dim]The source URL, retrieval time, and exact excerpt are retained in evidence provenance.[/]");
        AnsiConsole.WriteLine();
        return true;
    }

    private async ValueTask<bool> TryHandleAttributeAsync(string input)
    {
        var arguments = GetArguments(input, AttributeCommandName);
        if (string.IsNullOrWhiteSpace(arguments))
        {
            WriteError($"Usage: {AttributeCommandName} {{\"candidate\":\"Fernbank\",\"attributeName\":\"opening-hours\",\"value\":{{\"type\":\"timeWindow\",\"start\":\"08:00\",\"end\":\"18:00\"}},\"excerpt\":\"Fernbank is open from 08:00 to 18:00.\"}}");
            return true;
        }

        var observation = conversationContext.LatestToolObservation;
        if (observation is null ||
            !string.Equals(observation.ToolId, "web-page-fetch", StringComparison.OrdinalIgnoreCase))
        {
            WriteError("Fetch a public page with the web-page-fetch tool first. Only its latest observation can be reviewed.");
            return true;
        }

        if (!LatticeExternalPageObservationParser.TryParse(
                observation.Content,
                out var page,
                out var pageError))
        {
            WriteError(pageError ?? "The latest page observation could not be read.");
            return true;
        }

        if (!TryParseAttributeRequest(arguments, out var request, out var requestError))
        {
            WriteError(requestError ?? "The review request is invalid.");
            return true;
        }

        if (!LatticeReviewedCandidateAttributeEvidenceFactory.TryCreate(
                page!,
                request!.Value.Candidate,
                request.Value.AttributeName,
                request.Value.Value,
                request.Value.Excerpt,
                out var evidence,
                out var evidenceError))
        {
            WriteError(evidenceError ?? "The reviewed candidate attribute evidence could not be created.");
            return true;
        }

        AnsiConsole.MarkupLine("[bold yellow]Review this external candidate attribute[/]");
        AnsiConsole.WriteLine($"Candidate: {evidence!.Candidate}");
        AnsiConsole.WriteLine($"Attribute: {evidence.AttributeName}");
        AnsiConsole.WriteLine($"Typed value: {FormatValue(evidence.Value)}");
        AnsiConsole.WriteLine($"Source: {evidence.SourceUrl}");
        AnsiConsole.WriteLine($"Retrieved: {evidence.RetrievedAt:O}");
        AnsiConsole.WriteLine(evidence.FreshUntil is { } freshUntil
            ? $"Fresh until: {freshUntil:O}"
            : "Freshness: no cache expiry was supplied; this observation will remain unverified.");
        AnsiConsole.WriteLine($"Supporting excerpt: {evidence.SourceText}");
        AnsiConsole.WriteLine("Fetched text is untrusted. Check the page, typed value, and exact excerpt before accepting this claim.");
        AnsiConsole.Markup("[yellow]Type accept to add this evidence, or anything else to cancel: [/]" );

        if (!string.Equals(Console.ReadLine()?.Trim(), "accept", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[dim]Review cancelled; no candidate evidence was changed.[/]");
            AnsiConsole.WriteLine();
            return true;
        }

        var result = await session.AddReviewedCandidateAttributeEvidenceAsync(evidence);
        if (!result.IsSuccess)
        {
            WriteError(result.Error ?? "The reviewed candidate attribute evidence was rejected.");
            return true;
        }

        AnsiConsole.MarkupLine($"[bold green]Accepted[/] reviewed {Markup.Escape(evidence.AttributeName)} evidence for {Markup.Escape(result.Candidate ?? evidence.Candidate)}.");
        AnsiConsole.MarkupLine("[dim]The source URL, retrieval time, freshness deadline, and exact excerpt are retained in evidence provenance.[/]");
        AnsiConsole.WriteLine();
        return true;
    }

    private static bool TryParseAttributeRequest(
        string json,
        out (string Candidate, string AttributeName, ObjectiveConditionValue Value, string Excerpt)? request,
        out string? error)
    {
        request = null;
        error = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Review arguments must be a JSON object.";
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!names.Add(property.Name) ||
                    property.Name is not ("candidate" or "attributeName" or "value" or "excerpt"))
                {
                    error = "Review arguments contain a duplicate or unsupported property.";
                    return false;
                }
            }

            if (!root.TryGetProperty("candidate", out var candidate) ||
                candidate.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("attributeName", out var attributeName) ||
                attributeName.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("value", out var valueElement) ||
                !KnowledgePackLoader.TryParseTypedAttributeValue(valueElement, out var value, out var valueError) ||
                value is null ||
                !root.TryGetProperty("excerpt", out var excerpt) ||
                excerpt.ValueKind != JsonValueKind.String)
            {
                error = valueError ?? "Review arguments require string candidate and attributeName values, a supported typed value object, and a string excerpt.";
                return false;
            }

            request = (
                candidate.GetString() ?? string.Empty,
                attributeName.GetString() ?? string.Empty,
                value,
                excerpt.GetString() ?? string.Empty);
            return true;
        }
        catch (JsonException)
        {
            error = "Review arguments are not valid JSON.";
            return false;
        }
    }

    private static string FormatValue(ObjectiveConditionValue value) =>
        value switch
        {
            ObjectiveMoneyValue money =>
                $"{money.CurrencySymbol}{money.Amount.ToString(CultureInfo.InvariantCulture)}",
            ObjectiveTextValue text => text.Value,
            ObjectiveTextSetValue textSet => string.Join(", ", textSet.Values),
            ObjectiveTimeWindowValue window =>
                $"{window.Start.ToString("HH:mm", CultureInfo.InvariantCulture)}–{window.End.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            ObjectiveTimeSetValue timeSet =>
                string.Join(", ", timeSet.Times.Select(time => time.ToString("HH:mm", CultureInfo.InvariantCulture))),
            ObjectiveDurationValue duration => $"{duration.Minutes.ToString(CultureInfo.InvariantCulture)} minutes",
            ObjectiveSumValue sum => $"sum of {string.Join(", ", sum.Attributes)}",
            ObjectiveTemporalPlanValue plan => $"ordered plan with {plan.Stages.Length.ToString(CultureInfo.InvariantCulture)} stages",
            ObjectiveTemporalSequenceValue sequence =>
                $"sequence {sequence.EarliestStart:HH:mm}–{sequence.LatestEnd:HH:mm} with {sequence.Stages.Length.ToString(CultureInfo.InvariantCulture)} stages",
            _ => value.GetType().Name
        };

    private static bool IsCommand(string input, string commandName) =>
        input.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith(commandName + " ", StringComparison.OrdinalIgnoreCase);

    private static string GetArguments(string input, string commandName) =>
        input.Length > commandName.Length
            ? input[commandName.Length..].Trim()
            : string.Empty;

    private static bool TryParseRequest(
        string json,
        out (string Candidate, decimal Amount, string CurrencySymbol, string Excerpt)? request,
        out string? error)
    {
        request = null;
        error = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Review arguments must be a JSON object.";
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!names.Add(property.Name) ||
                    property.Name is not ("candidate" or "amount" or "currencySymbol" or "excerpt"))
                {
                    error = "Review arguments contain a duplicate or unsupported property.";
                    return false;
                }
            }

            if (!root.TryGetProperty("candidate", out var candidate) ||
                candidate.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("amount", out var amount) ||
                amount.ValueKind != JsonValueKind.Number ||
                !amount.TryGetDecimal(out var amountValue) ||
                !root.TryGetProperty("currencySymbol", out var currency) ||
                currency.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("excerpt", out var excerpt) ||
                excerpt.ValueKind != JsonValueKind.String)
            {
                error = "Review arguments require string candidate, numeric amount, string currencySymbol, and string excerpt values.";
                return false;
            }

            request = (
                candidate.GetString() ?? string.Empty,
                amountValue,
                currency.GetString() ?? string.Empty,
                excerpt.GetString() ?? string.Empty);
            return true;
        }
        catch (JsonException)
        {
            error = "Review arguments are not valid JSON.";
            return false;
        }
    }

    private static void WriteError(string message)
    {
        AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
        AnsiConsole.WriteLine(message);
        AnsiConsole.WriteLine();
    }
}
