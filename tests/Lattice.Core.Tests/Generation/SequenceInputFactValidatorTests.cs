using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceInputFactValidatorTests
{
    [Fact]
    public void Ordered_arithmetic_functions_can_call_prior_definitions_but_not_themselves()
    {
        var priorCall = new SemanticFunctionCallExpression(
            "first",
            [new SemanticNameExpression("value")]);
        var validSecondExpression = new SemanticBinaryExpression(
            SemanticExpressionOperator.Multiply,
            priorCall,
            new SemanticNumberExpression("2"));
        var validFacts = CreateFacts(validSecondExpression);
        var validation = CreateValidation();
        var pack = LoadPythonPack();

        Assert.True(SequenceInputFactValidator.TryValidate(
            [validation], validFacts, pack, out var validError), validError);

        var selfCall = new SemanticFunctionCallExpression(
            "second",
            [new SemanticNameExpression("value")]);
        var invalidSecondExpression = new SemanticBinaryExpression(
            SemanticExpressionOperator.Add,
            priorCall,
            selfCall);
        Assert.False(SequenceInputFactValidator.TryValidate(
            [validation], CreateFacts(invalidSecondExpression), pack, out _));
    }

    [Fact]
    public void Validates_structured_arithmetic_types_and_references_without_source_parsing()
    {
        var valid = new SemanticBinaryExpression(
            SemanticExpressionOperator.Add,
            new SemanticNameExpression("value"),
            new SemanticNumberExpression("2"));
        var invalidType = new SemanticBinaryExpression(
            SemanticExpressionOperator.Add,
            new SemanticNameExpression("value"),
            new SemanticTextExpression("two"));
        var missingReference = new SemanticBinaryExpression(
            SemanticExpressionOperator.Add,
            new SemanticNameExpression("missing"),
            new SemanticNumberExpression("2"));
        var facts = ImmutableArray.Create(
            new SequenceSemanticFact(
                "program.expression",
                ImmutableDictionary<string, string>.Empty)
            {
                Expressions = ImmutableDictionary<string, SemanticExpression>.Empty
                    .Add("value", valid)
            },
            new SequenceSemanticFact(
                "program.scope",
                ImmutableDictionary<string, string>.Empty)
            {
                Collections = ImmutableDictionary<string, ImmutableArray<string>>.Empty
                    .Add("parameters", ["value"])
            });
        var validation = new SequenceInputFactValidation(
            "expression-references-parameters",
            "arithmeticExpressionReferences",
            "program.expression",
            "value",
            "program.scope",
            "parameters");

        Assert.True(SequenceInputFactValidator.TryValidate([validation], facts, out var validError), validError);

        var invalidTypeFacts = facts.SetItem(
            0,
            facts[0] with
            {
                Expressions = ImmutableDictionary<string, SemanticExpression>.Empty
                    .Add("value", invalidType)
            });
        Assert.False(SequenceInputFactValidator.TryValidate([validation], invalidTypeFacts, out _));

        var missingReferenceFacts = facts.SetItem(
            0,
            facts[0] with
            {
                Expressions = ImmutableDictionary<string, SemanticExpression>.Empty
                    .Add("value", missingReference)
            });
        Assert.False(SequenceInputFactValidator.TryValidate([validation], missingReferenceFacts, out _));
    }

    private static ImmutableArray<SequenceSemanticFact> CreateFacts(
        SemanticExpression secondExpression)
    {
        var firstExpression = new SemanticBinaryExpression(
            SemanticExpressionOperator.Add,
            new SemanticNameExpression("value"),
            new SemanticNumberExpression("1"));
        var signature = new SequenceSemanticFact(
            "program.function-signatures",
            ImmutableDictionary<string, string>.Empty)
        {
            Collections = ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("name", ["first", "second"])
                .Add("parameters", ["value", "value"])
        };
        var returns = new SequenceSemanticFact(
            "program.function-returns",
            ImmutableDictionary<string, string>.Empty)
        {
            ExpressionCollections = ImmutableDictionary<string, ImmutableArray<SemanticExpression>>.Empty
                .Add("expression", [firstExpression, secondExpression])
        };

        return [signature, returns];
    }

    private static SequenceInputFactValidation CreateValidation() => new(
        "function-expressions-use-ordered-scopes",
        "orderedArithmeticFunctionExpressions",
        "program.function-returns",
        "expression",
        "program.function-signatures",
        "parameters",
        "program.function-signatures",
        "name");

    private static GenerationLanguagePack LoadPythonPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
