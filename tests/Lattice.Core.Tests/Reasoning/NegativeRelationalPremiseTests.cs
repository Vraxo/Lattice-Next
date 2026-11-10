using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class NegativeRelationalPremiseTests
{
    private readonly LceParser parser = new();
    private readonly ReasoningEngine engine = new();

    [Theory]
    [InlineData(
        "Every dog that does not like milk is friendly.",
        "forall(x, implies(and(dog(x), not(likes(x, milk))), friendly(x)))")]
    [InlineData(
        "All dogs that do not like milk are friendly.",
        "forall(x, implies(and(dog(x), not(likes(x, milk))), friendly(x)))")]
    [InlineData(
        "Every dog that does not like water is friendly.",
        "forall(x, implies(and(dog(x), not(likes(x, water))), friendly(x)))")]
    public void Parses_explicit_negative_fixed_object_relational_premises(
        string sentence,
        string expectedMeaning)
    {
        var result = parser.Parse(sentence);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expectedMeaning, SemanticFormatter.Format(result.Utterance!));
    }

    [Theory]
    [InlineData("Every dog that do not like milk is friendly.")]
    [InlineData("All dogs that does not like milk are friendly.")]
    [InlineData("Every dog that does not likes milk is friendly.")]
    [InlineData("Every dog that does not invent milk is friendly.")]
    [InlineData("There is a dog that does not like milk.")]
    [InlineData("The dog that does not like milk is friendly.")]
    public void Rejects_malformed_or_out_of_scope_negative_relational_clauses(string sentence)
    {
        var result = parser.Parse(sentence);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
    }

    [Fact]
    public void Applies_a_negative_relational_premise_from_explicit_negative_evidence()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that does not like milk is friendly.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido does not like milk.");

        var rule = Assert.Single(knowledgeBase.Rules);
        Assert.Equal("likes", Assert.Single(rule.NegativeRelationalPremises).Predicate.Value);

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog that does not like milk is friendly.", result.Proof!.SourceText);
        Assert.Equal("Fido is a dog.", result.Proof.Premise!.SourceText);

        var relation = Assert.Single(result.Proof.AdditionalPremises);
        Assert.Equal("likes", relation.Predicate.Predicate.Value);
        Assert.Equal("Fido does not like milk.", relation.SourceText);
        Assert.Equal(EvidencePolarity.Negative, relation.Polarity);
    }

    [Fact]
    public void Uses_negative_relational_evidence_for_a_fixed_object_relational_conclusion()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that does not like milk likes water.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido does not like milk.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like water?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog that does not like milk likes water.", result.Proof!.SourceText);
        var relation = Assert.Single(result.Proof.AdditionalPremises);
        Assert.Equal("Fido does not like milk.", relation.SourceText);
        Assert.Equal(EvidencePolarity.Negative, relation.Polarity);
    }

    [Fact]
    public void Does_not_treat_missing_or_positive_relations_as_negative_evidence()
    {
        var missing = new KnowledgeBase();
        Add(missing, "Every dog that does not like milk is friendly.");
        Add(missing, "Fido is a dog.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(missing, ParseQuestion("Is Fido friendly?")).TruthValue);

        var positiveOnly = new KnowledgeBase();
        Add(positiveOnly, "Every dog that does not like milk is friendly.");
        Add(positiveOnly, "Fido is a dog.");
        Add(positiveOnly, "Fido likes milk.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(positiveOnly, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    [Fact]
    public void Negative_relational_premises_match_the_same_subject_and_exact_object()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that does not like milk is friendly.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Rex does not like milk.");
        Add(knowledgeBase, "Fido does not like water.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.Unknown, result.TruthValue);
    }

    [Fact]
    public void Does_not_reuse_derived_relational_conclusions_as_relational_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog does not like milk.");
        Add(knowledgeBase, "Every dog that does not like milk is friendly.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.Unknown, result.TruthValue);
    }

    [Fact]
    public void Rejects_more_than_one_fixed_object_relational_premise_across_both_polarities()
    {
        var variable = new VariableTerm(new SemanticIdentifier("x"));
        var premises = new Conjunction(ImmutableArray.Create<SemanticFormula>(
            Unary("dog", variable),
            new Negation(Relation("likes", variable, "milk")),
            Relation("chases", variable, "cats")));
        var utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            new UniversalQuantification(
                variable,
                new Implication(premises, Unary("friendly", variable))),
            "Every dog with the specified conditions is friendly.");
        var knowledgeBase = new KnowledgeBase();

        var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("supported", error);
        Assert.Empty(knowledgeBase.Rules);
    }

    [Fact]
    public void Factory_preserves_the_fixed_object_in_a_negative_relational_premise()
    {
        var milkRule = CreateRule("Every dog that does not like milk is friendly.");
        var waterRule = CreateRule("Every dog that does not like water is friendly.");

        var milkObject = Assert.IsType<IndividualTerm>(
            Assert.Single(milkRule.NegativeRelationalPremises).Arguments[1]);
        var waterObject = Assert.IsType<IndividualTerm>(
            Assert.Single(waterRule.NegativeRelationalPremises).Arguments[1]);

        Assert.Equal("milk", milkObject.Identifier.Value);
        Assert.Equal("water", waterObject.Identifier.Value);
        Assert.NotEqual(milkObject, waterObject);
    }

    [Fact]
    public void Knowledge_base_keeps_distinct_negative_relational_premises_when_semantics_are_supplied_directly()
    {
        var knowledgeBase = new KnowledgeBase();

        foreach (var objectIdentifier in new[] { "milk", "water" })
        {
            var variable = new VariableTerm(new SemanticIdentifier("x"));
            var premises = new Conjunction(ImmutableArray.Create<SemanticFormula>(
                Unary("dog", variable),
                new Negation(Relation("likes", variable, objectIdentifier))));
            var utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                new UniversalQuantification(
                    variable,
                    new Implication(premises, Unary("friendly", variable))),
                $"Every dog that does not like {objectIdentifier} is friendly.");

            var accepted = knowledgeBase.TryAdd(utterance, out var kind, out var error);

            Assert.True(accepted, error);
            Assert.Equal(KnowledgeEntryKind.Rule, kind);
        }

        Assert.Equal(2, knowledgeBase.Rules.Count);
        Assert.Contains(knowledgeBase.Rules, rule =>
            Assert.IsType<IndividualTerm>(Assert.Single(rule.NegativeRelationalPremises).Arguments[1])
                .Identifier.Value == "milk");
        Assert.Contains(knowledgeBase.Rules, rule =>
            Assert.IsType<IndividualTerm>(Assert.Single(rule.NegativeRelationalPremises).Arguments[1])
                .Identifier.Value == "water");
    }

    [Fact]
    public void Keeps_rules_with_distinct_negative_relational_premises_separate()
    {
        const string milkSentence = "Every dog that does not like milk is friendly.";
        const string waterSentence = "Every dog that does not like water is friendly.";
        var milkRule = parser.Parse(milkSentence);
        var waterRule = parser.Parse(waterSentence);

        Assert.True(milkRule.IsSuccess, milkRule.Error);
        Assert.True(waterRule.IsSuccess, waterRule.Error);
        Assert.Equal(
            "forall(x, implies(and(dog(x), not(likes(x, milk))), friendly(x)))",
            SemanticFormatter.Format(milkRule.Utterance!));
        Assert.Equal(
            "forall(x, implies(and(dog(x), not(likes(x, water))), friendly(x)))",
            SemanticFormatter.Format(waterRule.Utterance!));

        var knowledgeBase = new KnowledgeBase();
        AddParsed(knowledgeBase, milkRule.Utterance!);
        AddParsed(knowledgeBase, waterRule.Utterance!);
        AddParsed(knowledgeBase, milkRule.Utterance!);

        Assert.Equal(2, knowledgeBase.Rules.Count);
        var milkRuleInKnowledgeBase = knowledgeBase.Rules.Single(rule =>
            rule.NegativeRelationalPremises.Length == 1 &&
            rule.NegativeRelationalPremises[0].Arguments[1] is IndividualTerm individual &&
            individual.Identifier.Value == "milk");
        Assert.Equal(2, milkRuleInKnowledgeBase.Sources.Length);
    }

    private UniversalRule CreateRule(string sentence)
    {
        var utterance = Parse(sentence);
        var created = UniversalRuleFactory.TryCreate(
            utterance.Meaning,
            utterance.SourceText,
            KnowledgeSourceKind.UserAssertion,
            null,
            out var rule,
            out _);

        Assert.True(created);
        return rule;
    }

    private SemanticUtterance Parse(string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return result.Utterance!;
    }

    private static void AddParsed(KnowledgeBase knowledgeBase, SemanticUtterance utterance)
    {
        var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);
        Assert.True(accepted, error);
    }

    private void Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Utterance);

        var accepted = knowledgeBase.TryAdd(result.Utterance, out _, out var error);

        Assert.True(accepted, error);
    }

    private PredicateApplication ParseQuestion(string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        var utterance = result.Utterance!;
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<PredicateApplication>(utterance.Meaning);
    }

    private static PredicateApplication Unary(string predicate, SemanticTerm subject) =>
        new(
            new SemanticIdentifier(predicate),
            ImmutableArray.Create(subject));

    private static PredicateApplication Relation(
        string predicate,
        SemanticTerm subject,
        string objectIdentifier) =>
        new(
            new SemanticIdentifier(predicate),
            ImmutableArray.Create<SemanticTerm>(
                subject,
                new IndividualTerm(new SemanticIdentifier(objectIdentifier))));
}
