using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticProgramRequirementTests
{
    [Theory]
    [InlineData("numbers", SemanticCollectionItemKind.Number)]
    [InlineData("names", SemanticCollectionItemKind.Text)]
    public void Bindings_keep_the_name_and_element_kind_without_contents(
        string name,
        SemanticCollectionItemKind elementKind)
    {
        var program = new SemanticProgram([
            new OutputRequirement(new SemanticNameExpression(name))
        ])
        {
            Collections = [new CollectionBinding(name, elementKind)]
        };

        Assert.True(SemanticProgramValidator.IsValid(program));
        var binding = Assert.Single(program.Collections);
        Assert.Equal(name, binding.Name);
        Assert.Equal(elementKind, binding.ElementKind);
    }

    [Fact]
    public void Rejects_a_binding_without_a_usable_name_or_element_kind()
    {
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([
            new OutputRequirement(new SemanticTextExpression("ready"))
        ])
        {
            Collections = [new CollectionBinding("2 numbers", SemanticCollectionItemKind.Number)]
        }));

        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([
            new OutputRequirement(new SemanticTextExpression("ready"))
        ])
        {
            Collections = [new CollectionBinding("numbers", (SemanticCollectionItemKind) 99)]
        }));
    }

    [Fact]
    public void Represents_an_iteration_item_binding_with_its_source_and_body()
    {
        var program = new SemanticProgram([
            new IterationRequirement(
                "score",
                new SemanticNameExpression("scores"),
                [new OutputRequirement(new SemanticNameExpression("score"))])
        ])
        {
            Collections = [new CollectionBinding("scores", SemanticCollectionItemKind.Number)]
        };

        Assert.True(SemanticProgramValidator.IsValid(program));
        var iteration = Assert.IsType<IterationRequirement>(Assert.Single(program.Requirements));
        Assert.Equal("score", iteration.ItemName);
        Assert.Equal(new SemanticNameExpression("scores"), iteration.Source);
        Assert.Equal(
            new OutputRequirement(new SemanticNameExpression("score")),
            Assert.Single(iteration.Body));
    }

    [Fact]
    public void Rejects_an_iteration_without_a_body_or_with_an_invalid_source()
    {
        var withoutBody = new SemanticProgram([
            new IterationRequirement("score", new SemanticNameExpression("scores"), [])
        ]);
        Assert.False(SemanticProgramValidator.IsValid(withoutBody));

        var invalidSource = new SemanticProgram([
            new IterationRequirement(
                "score",
                new SemanticNameExpression(string.Empty),
                [new OutputRequirement(new SemanticNameExpression("score"))])
        ]);
        Assert.False(SemanticProgramValidator.IsValid(invalidSource));

        var invalidItem = new SemanticProgram([
            new IterationRequirement(
                "not an identifier",
                new SemanticNameExpression("scores"),
                [new OutputRequirement(new SemanticNameExpression("scores"))])
        ]);
        Assert.False(SemanticProgramValidator.IsValid(invalidItem));
    }

    [Fact]
    public void Represents_dictionary_key_and_value_bindings_separately()
    {
        var program = new SemanticProgram([
            new DictionaryIterationRequirement(
                "product",
                "price",
                new SemanticNameExpression("prices"),
                [
                    new OutputRequirement(new SemanticNameExpression("product")),
                    new OutputRequirement(new SemanticNameExpression("price"))
                ])
        ])
        {
            Collections = [new CollectionBinding("prices", SemanticCollectionItemKind.Text)]
        };

        Assert.True(SemanticProgramValidator.IsValid(program));
        var iteration = Assert.IsType<DictionaryIterationRequirement>(Assert.Single(program.Requirements));
        Assert.Equal("product", iteration.KeyName);
        Assert.Equal("price", iteration.ValueName);
        Assert.Equal(2, iteration.Body.Length);
    }

    [Theory]
    [InlineData("item", "item")]
    [InlineData("item", "")]
    public void Rejects_dictionary_bindings_that_cannot_be_told_apart(string keyName, string valueName)
    {
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([
            new DictionaryIterationRequirement(
                keyName,
                valueName,
                new SemanticNameExpression("prices"),
                [new OutputRequirement(new SemanticNameExpression("prices"))])
        ])));
    }

    [Fact]
    public void Rejects_unknown_requirement_types_and_deep_nesting()
    {
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([new UnknownRequirement()])));

        ProgramRequirement requirement = new OutputRequirement(new SemanticTextExpression("deep"));
        for (var depth = 0; depth < SemanticProgramValidator.MaximumDepth + 1; depth++)
        {
            requirement = new ConditionalRequirement(
                new SemanticBooleanExpression(true),
                [requirement],
                []);
        }

        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([requirement])));
    }

    [Fact]
    public void Rejects_an_empty_program()
    {
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([])));
        Assert.False(SemanticProgramValidator.IsValid(null));
    }

    private sealed record UnknownRequirement : ProgramRequirement;
}
