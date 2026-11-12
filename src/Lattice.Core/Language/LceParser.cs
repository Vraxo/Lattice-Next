using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

public sealed class LceParser
{
    private static readonly Regex ObjectiveRequestPattern = new(
        @"(?:^|[.!?]\s*)Help me\s+(?<goal>[^.!?]+)[.!?]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveCandidateMoneyPattern = new(
        @"^(?<candidate>[A-Z][A-Za-z0-9-]*(?:\s+[A-Za-z0-9][A-Za-z0-9-]*)*)\s+costs\s+(?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveCandidateTextPattern = new(
        @"^(?<candidate>[A-Z][A-Za-z0-9-]*(?:\s+[A-Za-z0-9][A-Za-z0-9-]*)*)['’]s\s+(?<attribute>[A-Za-z][A-Za-z -]*)\s+is\s+(?<value>[^.!?]+)[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveMoneyResponsePattern = new(
        @"^(?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)(?:\s+total)?\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveMoneySpendResponsePattern = new(
        @"^I can spend up to (?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)(?:\s+total)?\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BringLunchPattern = new(
        @"^I'll bring lunch\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StartingLocationPattern = new(
        @"\b(?:I'm starting at|my starting point is)\s+(?<location>[^,.!?]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TimeWindowPattern = new(
        @"\bI can (?:(?:leave at (?<start>\d{1,2}:\d{2}) and be back by (?<end>\d{1,2}:\d{2}))|(?:be back by (?<reverseEnd>\d{1,2}:\d{2}) and leave at (?<reverseStart>\d{1,2}:\d{2})))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NoBusPattern = new(
        @"\bI don't want to take a bus\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WalkingOnlyPattern = new(
        @"^Walking only\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectivePreferenceRevisionPattern = new(
        @"^(?:Actually,\s*)?(?:I would prefer|I prefer|I care about)\s+[^.!?]+[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveBudgetRevisionPattern = new(
        @"^(?:Actually,\s*)?(?:my\s+)?budget\b[^.!?]*[$€£]\s*\d[\d,]*(?:\.\d{1,2})?[^.!?]*[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectivePreferenceAdditionPattern = new(
        @"^(?:Also,\s*(?:I care about|I prefer)|I also care about|I also prefer)\s+(?<value>[^.!?]+)[.!?]$|^Add\s+(?<add>.+?)\s+to my preferences[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectivePreferenceRemovalPattern = new(
        @"^Remove\s+(?<value>.+?)\s+from my preferences[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UniversalRulePattern = new(
        @"^Every\s+(?<description>[a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]*)*)\s+is\s+(?:a|an)\s+(?<supertype>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex UniversalRelationRulePattern = new(
        @"^Every\s+(?<description>[a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]*)*)\s+(?<predicate>(?!is\b)[a-z][a-z0-9-]*s)\s+(?<object>[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*|(?!the\b)[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ExistentialAssertionPattern = new(
        @"^(?:A|An)\s+(?<description>[a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]*)*)\s+exists\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IndefiniteAssertionPattern = new(
        @"^(?:A|An)\s+(?<description>[a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]*)*)\s+is\s+(?:(?:a|an)\s+)?(?<property>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ExistentialQuestionPattern = new(
        @"^Does\s+(?:a|an)\s+(?<description>[a-z][a-z0-9-]*(?:\s+[a-z][a-z0-9-]*)*)\s+exist\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DefiniteExistentialQuestionPattern = new(
        @"^Does\s+[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*\s+exist\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex AssertionPattern = new(
        @"^(?<individual>(?!It\s)(?:[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*))\s+is\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PropertyAssertionPattern = new(
        @"^(?<individual>[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*)\s+is\s+(?<property>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PronounAssertionPattern = new(
        @"^It\s+is\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex RelationAssertionPattern = new(
        @"^(?<individual>It|[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*)\s+(?<predicate>(?!is\b)[a-z][a-z0-9-]*s)\s+(?<object>[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*|(?!the\b)[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex QuestionPattern = new(
        @"^Is\s+(?<individual>(?!It\s)(?:[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*))\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PropertyQuestionPattern = new(
        @"^Is\s+(?<individual>[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*)\s+(?<property>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PronounQuestionPattern = new(
        @"^Is\s+it\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex RelationQuestionPattern = new(
        @"^Does\s+(?<individual>it|[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*)\s+(?<predicate>(?!be\b|exist\b)[a-z][a-z0-9-]*)\s+(?<object>[A-Z][a-zA-Z0-9-]*|[Tt]he\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*|(?!the\b)[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex NegatedRelativeClausePattern = new(
        @"\bthat\s+(?:(?:is|are)|(?:does|do))\s+not\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult.Failure("Input is empty.");
        }

        var originalSource = input.Trim();
        var source = InputNormalizer.Normalize(originalSource).Trim();

        if (source.Length == 0)
        {
            return ParseResult.Failure("Input is empty.");
        }

        if (TryParseObjectiveCandidateInformation(source, originalSource, out var candidateInformationUtterance))
        {
            return ParseResult.Success(candidateInformationUtterance!);
        }

        if (TryParseObjectiveInformation(source, originalSource, out var informationUtterance))
        {
            return ParseResult.Success(informationUtterance!);
        }

        if (TryParseObjectiveConditionUpdates(source, originalSource, out var conditionUpdateUtterance))
        {
            return ParseResult.Success(conditionUpdateUtterance!);
        }

        if (TryParseObjective(source, originalSource, out var objectiveUtterance))
        {
            return ParseResult.Success(objectiveUtterance!);
        }

        if (NativeEnglishGrammar.TryParse(source, originalSource, out var nativeUtterance))
        {
            return ParseResult.Success(nativeUtterance!);
        }

        if (NegatedRelativeClausePattern.IsMatch(source))
        {
            return ParseResult.Failure(
                "Explicitly negated relative conditions are currently supported only in universal rules.");
        }

        var universalMatch = UniversalRulePattern.Match(source);

        if (universalMatch.Success)
        {
            var variable = new VariableTerm(new SemanticIdentifier("x"));
            var premises = CreateUniversalPremises(
                universalMatch.Groups["description"].Value,
                variable);
            SemanticFormula premise = premises.Length == 1
                ? premises[0]
                : new Conjunction(premises.Cast<SemanticFormula>().ToImmutableArray());
            var supertype = CreatePredicate(universalMatch.Groups["supertype"].Value, variable);
            var implication = new Implication(premise, supertype);
            var meaning = new UniversalQuantification(variable, implication);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var universalRelationMatch = UniversalRelationRulePattern.Match(source);

        if (universalRelationMatch.Success)
        {
            var variable = new VariableTerm(new SemanticIdentifier("x"));
            var premises = CreateUniversalPremises(
                universalRelationMatch.Groups["description"].Value,
                variable);
            SemanticFormula premise = premises.Length == 1
                ? premises[0]
                : new Conjunction(premises.Cast<SemanticFormula>().ToImmutableArray());
            var conclusion = new PredicateApplication(
                CreateIdentifier(universalRelationMatch.Groups["predicate"].Value),
                ImmutableArray.Create<SemanticTerm>(
                    variable,
                    CreateSubjectTerm(universalRelationMatch.Groups["object"].Value)));
            var meaning = new UniversalQuantification(
                variable,
                new Implication(premise, conclusion));

            return ParseResult.Success(new SemanticUtterance(
                UtteranceKind.Assertion,
                meaning,
                originalSource));
        }

        var existentialAssertionMatch = ExistentialAssertionPattern.Match(source);

        if (existentialAssertionMatch.Success)
        {
            var meaning = CreateExistentialFormula(
                existentialAssertionMatch.Groups["description"].Value,
                null);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var indefiniteAssertionMatch = IndefiniteAssertionPattern.Match(source);

        if (indefiniteAssertionMatch.Success)
        {
            var meaning = CreateExistentialFormula(
                indefiniteAssertionMatch.Groups["description"].Value,
                indefiniteAssertionMatch.Groups["property"].Value);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var pronounAssertionMatch = PronounAssertionPattern.Match(source);

        if (pronounAssertionMatch.Success)
        {
            var meaning = CreatePredicate(
                pronounAssertionMatch.Groups["concept"].Value,
                CreateUnresolvedReference());

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var assertionMatch = AssertionPattern.Match(source);

        if (assertionMatch.Success)
        {
            var meaning = CreatePredicate(
                assertionMatch.Groups["concept"].Value,
                CreateSubjectTerm(assertionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var propertyAssertionMatch = PropertyAssertionPattern.Match(source);

        if (propertyAssertionMatch.Success)
        {
            var meaning = CreatePredicate(
                propertyAssertionMatch.Groups["property"].Value,
                CreateSubjectTerm(propertyAssertionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var relationAssertionMatch = RelationAssertionPattern.Match(source);

        if (relationAssertionMatch.Success)
        {
            var meaning = CreateRelationPredicate(
                relationAssertionMatch.Groups["predicate"].Value,
                relationAssertionMatch.Groups["individual"].Value,
                relationAssertionMatch.Groups["object"].Value);

            if (HasMultipleUnresolvedReferences(meaning))
            {
                return ParseResult.Failure(
                    "Relational sentences currently support only one 'it' reference at a time.");
            }

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var existentialQuestionMatch = ExistentialQuestionPattern.Match(source);

        if (existentialQuestionMatch.Success)
        {
            var meaning = CreateExistentialFormula(
                existentialQuestionMatch.Groups["description"].Value,
                null);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        if (DefiniteExistentialQuestionPattern.IsMatch(source))
        {
            return ParseResult.Failure(
                "Existence questions currently require an indefinite description beginning with 'a' or 'an'.");
        }

        var pronounQuestionMatch = PronounQuestionPattern.Match(source);

        if (pronounQuestionMatch.Success)
        {
            var meaning = CreatePredicate(
                pronounQuestionMatch.Groups["concept"].Value,
                CreateUnresolvedReference());

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        var questionMatch = QuestionPattern.Match(source);

        if (questionMatch.Success)
        {
            var meaning = CreatePredicate(
                questionMatch.Groups["concept"].Value,
                CreateSubjectTerm(questionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        var propertyQuestionMatch = PropertyQuestionPattern.Match(source);

        if (propertyQuestionMatch.Success)
        {
            var meaning = CreatePredicate(
                propertyQuestionMatch.Groups["property"].Value,
                CreateSubjectTerm(propertyQuestionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        var relationQuestionMatch = RelationQuestionPattern.Match(source);

        if (relationQuestionMatch.Success)
        {
            var meaning = CreateRelationPredicate(
                InflectThirdPersonSingular(relationQuestionMatch.Groups["predicate"].Value),
                relationQuestionMatch.Groups["individual"].Value,
                relationQuestionMatch.Groups["object"].Value);

            if (HasMultipleUnresolvedReferences(meaning))
            {
                return ParseResult.Failure(
                    "Relational sentences currently support only one 'it' reference at a time.");
            }

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        return ParseResult.Failure(
            "That sentence is outside LCE 0.9. Use :help to see the supported forms.");
    }

    public TextParseResult ParseText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) ||
            string.IsNullOrWhiteSpace(InputNormalizer.Normalize(input)))
        {
            return TextParseResult.Failure("Input is empty.");
        }

        var source = input.Trim();

        if (TryParseObjective(source, source, out var objectiveUtterance))
        {
            return TextParseResult.Success(ImmutableArray.Create(objectiveUtterance!));
        }

        var utterances = ImmutableArray.CreateBuilder<SemanticUtterance>();
        var sentenceStart = 0;

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] is not ('.' or '?'))
            {
                continue;
            }

            var sentence = source[sentenceStart..(index + 1)].Trim();
            var result = Parse(sentence);

            if (!result.IsSuccess || result.Utterance is null)
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count + 1}: {result.Error ?? "The sentence could not be parsed."}");
            }

            utterances.Add(result.Utterance);
            sentenceStart = index + 1;

            if (sentenceStart < source.Length &&
                !InputNormalizer.IsTextSeparator(source[sentenceStart]))
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count} must be separated from the next sentence by whitespace.");
            }

            while (sentenceStart < source.Length &&
                   InputNormalizer.IsTextSeparator(source[sentenceStart]))
            {
                sentenceStart++;
            }
        }

        if (sentenceStart < source.Length)
        {
            return TextParseResult.Failure(
                $"Sentence {utterances.Count + 1} is incomplete. Every sentence must end with a period or question mark.");
        }

        if (utterances.Count == 0)
        {
            return TextParseResult.Failure("Input does not contain a complete sentence.");
        }

        return TextParseResult.Success(utterances.ToImmutable());
    }

    private static bool TryParseObjectiveCandidateInformation(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var moneyMatch = ObjectiveCandidateMoneyPattern.Match(source);

        if (moneyMatch.Success &&
            decimal.TryParse(
                moneyMatch.Groups["amount"].Value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            utterance = new SemanticUtterance(
                UtteranceKind.ObjectiveCandidateInformation,
                new ObjectiveCandidateInformationFormula(
                    new SemanticIdentifier(moneyMatch.Groups["candidate"].Value.ToLowerInvariant()),
                    new SemanticIdentifier("total-cost"),
                    new ObjectiveMoneyValue(amount, moneyMatch.Groups["symbol"].Value)),
                sourceText.Trim());
            return true;
        }

        var textMatch = ObjectiveCandidateTextPattern.Match(source);

        if (!textMatch.Success)
        {
            utterance = null;
            return false;
        }

        var attributeName = Regex.Replace(
            textMatch.Groups["attribute"].Value.Trim(),
            @"\s+",
            "-").ToLowerInvariant();

        utterance = new SemanticUtterance(
            UtteranceKind.ObjectiveCandidateInformation,
            new ObjectiveCandidateInformationFormula(
                new SemanticIdentifier(textMatch.Groups["candidate"].Value.ToLowerInvariant()),
                new SemanticIdentifier(attributeName),
                new ObjectiveTextValue(textMatch.Groups["value"].Value.Trim())),
            sourceText.Trim());
        return true;
    }

    private static bool TryParseObjectiveInformation(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var match = ObjectiveMoneyResponsePattern.Match(source);

        if (!match.Success)
        {
            match = ObjectiveMoneySpendResponsePattern.Match(source);
        }

        if (!match.Success ||
            !decimal.TryParse(
                match.Groups["amount"].Value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.ObjectiveInformation,
            new ObjectiveInformationFormula(new ObjectiveMoneyValue(
                amount,
                match.Groups["symbol"].Value)),
            sourceText.Trim());
        return true;
    }

    private static bool TryParseObjectiveConditionUpdates(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var conditions = ImmutableArray.CreateBuilder<ObjectiveCondition>();
        var preferenceEdits = ImmutableArray.CreateBuilder<ObjectivePreferenceEdit>();
        var isPreferenceRevision = ObjectivePreferenceRevisionPattern.IsMatch(source);
        var isBudgetRevision = ObjectiveBudgetRevisionPattern.IsMatch(source);
        var preferenceAddition = ObjectivePreferenceAdditionPattern.Match(source);
        var preferenceRemoval = ObjectivePreferenceRemovalPattern.Match(source);

        if (preferenceAddition.Success || preferenceRemoval.Success)
        {
            var match = preferenceAddition.Success ? preferenceAddition : preferenceRemoval;
            var value = match.Groups["value"].Success
                ? match.Groups["value"].Value
                : match.Groups["add"].Value;
            var parsedPreferences = ObjectiveConditionParser.Parse($"I care about {value}.");
            var values = parsedPreferences
                .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
                .OfType<ObjectiveCondition>()
                .SelectMany(condition => condition.Value is ObjectiveTextSetValue set
                    ? set.Values
                    : ImmutableArray<string>.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            if (!values.IsDefaultOrEmpty)
            {
                preferenceEdits.Add(new ObjectivePreferenceEdit(
                    preferenceAddition.Success
                        ? ObjectivePreferenceEditKind.Add
                        : ObjectivePreferenceEditKind.Remove,
                    values,
                    sourceText));
            }
        }

        if (isPreferenceRevision || isBudgetRevision)
        {
            conditions.AddRange(ObjectiveConditionParser.Parse(source).Where(condition =>
                (isPreferenceRevision && condition.Role == ObjectiveConditionRole.Preference) ||
                (isBudgetRevision && condition.Name == "spending-limit")));
        }

        if (BringLunchPattern.IsMatch(source))
        {
            conditions.Add(new ObjectiveCondition(
                "meal-plan",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue("bring-lunch"),
                sourceText)
            {
                // The user's meal plan is context for the cost calculation, not a candidate property.
                TargetAttribute = null
            });
        }

        var locationMatch = StartingLocationPattern.Match(source);

        if (locationMatch.Success)
        {
            conditions.Add(new ObjectiveCondition(
                "origin",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue(locationMatch.Groups["location"].Value.Trim()),
                sourceText)
            {
                TargetAttribute = "origin"
            });
        }

        var timeMatch = TimeWindowPattern.Match(source);
        var startText = timeMatch.Groups["start"].Success
            ? timeMatch.Groups["start"].Value
            : timeMatch.Groups["reverseStart"].Value;
        var endText = timeMatch.Groups["end"].Success
            ? timeMatch.Groups["end"].Value
            : timeMatch.Groups["reverseEnd"].Value;

        if (timeMatch.Success &&
            TimeOnly.TryParse(
                startText,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var start) &&
            TimeOnly.TryParse(
                endText,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var end))
        {
            conditions.Add(new ObjectiveCondition(
                "time-window",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.CanFit,
                new ObjectiveTimeWindowValue(start, end),
                sourceText)
            {
                TargetAttribute = "temporal-plan"
            });
        }

        if (NoBusPattern.IsMatch(source))
        {
            conditions.Add(new ObjectiveCondition(
                "transport-bus",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Excludes,
                new ObjectiveTextValue("bus"),
                sourceText)
            {
                TargetAttribute = "transport-mode"
            });
        }

        if (WalkingOnlyPattern.IsMatch(source))
        {
            conditions.Add(new ObjectiveCondition(
                "transport-mode",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue("walking"),
                sourceText)
            {
                TargetAttribute = "transport-mode"
            });
        }

        if (conditions.Count == 0 && preferenceEdits.Count == 0)
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.ObjectiveConditionUpdate,
            new ObjectiveConditionUpdateFormula(conditions.ToImmutable())
            {
                PreferenceEdits = preferenceEdits.ToImmutable()
            },
            sourceText.Trim());
        return true;
    }

    private static bool TryParseObjective(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var match = ObjectiveRequestPattern.Match(source);

        if (!match.Success)
        {
            utterance = null;
            return false;
        }

        var goal = match.Groups["goal"].Value.Trim();

        if (goal.Length == 0)
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Objective,
            new ObjectiveFormula(goal)
            {
                Conditions = ObjectiveConditionParser.Parse(sourceText)
            },
            sourceText.Trim());
        return true;
    }

    private static ExistentialQuantification CreateExistentialFormula(
        string description,
        string? additionalProperty)
    {
        var variable = new VariableTerm(new SemanticIdentifier("x"));
        var predicates = description
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => CreatePredicate(word, variable))
            .ToImmutableArray();

        var conditions = additionalProperty is null
            ? predicates
            : predicates.Add(CreatePredicate(additionalProperty, variable));

        SemanticFormula body = conditions.Length == 1
            ? conditions[0]
            : new Conjunction(conditions.Cast<SemanticFormula>().ToImmutableArray());

        return new ExistentialQuantification(variable, body);
    }

    private static ImmutableArray<PredicateApplication> CreateUniversalPremises(
        string description,
        VariableTerm variable) =>
        description
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => CreatePredicate(word, variable))
            .ToImmutableArray();

    private static PredicateApplication CreatePredicate(string predicate, SemanticTerm argument) =>
        new(
            CreateIdentifier(predicate),
            ImmutableArray.Create(argument));

    private static PredicateApplication CreateRelationPredicate(
        string predicate,
        string individual,
        string objectValue) =>
        new(
            CreateIdentifier(predicate),
            ImmutableArray.Create<SemanticTerm>(
                CreateRelationSubjectTerm(individual),
                CreateRelationObjectTerm(objectValue)));

    private static SemanticTerm CreateRelationSubjectTerm(string value) =>
        value is "It" or "it"
            ? CreateUnresolvedReference()
            : CreateSubjectTerm(value);

    private static SemanticTerm CreateRelationObjectTerm(string value) =>
        value == "it"
            ? CreateUnresolvedReference()
            : CreateSubjectTerm(value);

    private static bool HasMultipleUnresolvedReferences(PredicateApplication predicate) =>
        predicate.Arguments.Count(argument => argument is UnresolvedReferenceTerm) > 1;

    private static string InflectThirdPersonSingular(string verb)
    {
        if (verb is "have" or "do" or "go")
        {
            return verb switch
            {
                "have" => "has",
                "do" => "does",
                "go" => "goes",
                _ => verb
            };
        }

        if (verb.Length > 1 &&
            verb[^1] == 'y' &&
            verb[^2] is not ('a' or 'e' or 'i' or 'o' or 'u'))
        {
            return $"{verb[..^1]}ies";
        }

        if (verb.EndsWith("s", StringComparison.Ordinal) ||
            verb.EndsWith("x", StringComparison.Ordinal) ||
            verb.EndsWith("z", StringComparison.Ordinal) ||
            verb.EndsWith("ch", StringComparison.Ordinal) ||
            verb.EndsWith("sh", StringComparison.Ordinal) ||
            verb.EndsWith("o", StringComparison.Ordinal))
        {
            return $"{verb}es";
        }

        return $"{verb}s";
    }

    private static SemanticTerm CreateSubjectTerm(string value)
    {
        if (!value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return new IndividualTerm(CreateIdentifier(value));
        }

        var words = value[4..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var modifiers = words[..^1]
            .Select(CreateIdentifier)
            .ToImmutableArray();

        return new DescriptiveIndividualTerm(
            CreateIdentifier(words[^1]),
            modifiers);
    }

    private static UnresolvedReferenceTerm CreateUnresolvedReference() =>
        new(new SemanticIdentifier("it"));

    private static SemanticIdentifier CreateIdentifier(string value) =>
        new(value.ToLowerInvariant());
}
