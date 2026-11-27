using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticProgramRequirementTests
{
    [Fact]
    public void Accepts_a_list_literal_source_with_signed_and_decimal_items()
    {
        var program = new SemanticProgram([
            new AccumulationRequirement(
                "result",
                SemanticCollectionItemKind.Number,
                "value",
                new SemanticListLiteralExpression([
                    new SemanticUnaryExpression(
                        SemanticExpressionOperator.Negative,
                        new SemanticNumberExpression("2")),
                    new SemanticNumberExpression("4.5"),
                    new SemanticNumberExpression("1.5")
                ]),
                new SemanticNumberExpression("0"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression("result"),
                    new SemanticNameExpression("value"))),
            new OutputRequirement(new SemanticNameExpression("result"))
        ]);

        Assert.True(
            SemanticProgramValidator.IsValid(program),
            "source=" + SemanticExpressionAnalysis.IsValid(program.Requirements[0] is AccumulationRequirement a ? a.Source : new SemanticNumberExpression("0")) +
            " update=" + SemanticExpressionAnalysis.IsValid(program.Requirements[0] is AccumulationRequirement b ? b.Update : new SemanticNumberExpression("0")) +
            " initial=" + SemanticExpressionAnalysis.IsValid(program.Requirements[0] is AccumulationRequirement c ? c.InitialValue : new SemanticNumberExpression("0")) +
            " output=" + SemanticExpressionAnalysis.IsValid(((OutputRequirement)program.Requirements[1]).Value));
    }

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
    public void Accepts_a_continue_inside_a_loop_and_rejects_it_outside_one()
    {
        var insideLoop = new SemanticProgram([
            new IterationRequirement(
                "score",
                new SemanticNameExpression("scores"),
                [new ContinueRequirement()])
        ]);
        Assert.True(SemanticProgramValidator.IsValid(insideLoop));

        var atTopLevel = new SemanticProgram([new ContinueRequirement()]);
        Assert.False(SemanticProgramValidator.IsValid(atTopLevel));

        var afterTheLoop = new SemanticProgram([
            new IterationRequirement(
                "score",
                new SemanticNameExpression("scores"),
                [new OutputRequirement(new SemanticNameExpression("score"))]),
            new ContinueRequirement()
        ]);
        Assert.False(SemanticProgramValidator.IsValid(afterTheLoop));

        var insideADeclarationInALoop = new SemanticProgram([
            new IterationRequirement(
                "score",
                new SemanticNameExpression("scores"),
                [new FunctionRequirement(
                    "report",
                    [],
                    null,
                    [new ContinueRequirement()])])
        ]);
        Assert.False(SemanticProgramValidator.IsValid(insideADeclarationInALoop));
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

    [Theory]
    [InlineData("scores", SemanticCollectionItemKind.Number, 3)]
    [InlineData("scores", SemanticCollectionItemKind.Number, 5)]
    [InlineData("names", SemanticCollectionItemKind.Text, 2)]
    public void Projections_keep_the_binding_while_contents_vary(
        string name,
        SemanticCollectionItemKind elementKind,
        int itemCount)
    {
        var items = Enumerable.Range(1, itemCount)
            .Select(index => new SemanticCollectionItem(
                elementKind,
                elementKind == SemanticCollectionItemKind.Number ? index.ToString() : $"item{index}"))
            .ToImmutableArray();
        var contents = new SemanticCollection(items);

        var program = new SemanticProgram([
            new OutputRequirement(new SemanticNameExpression(name))
        ])
        {
            Collections = [new CollectionBinding(name, elementKind)]
        };

        Assert.True(SemanticCollectionValidator.IsValid(contents));
        Assert.Equal(itemCount, contents.Items.Length);
        Assert.True(SemanticProgramValidator.IsValid(program));
        var binding = Assert.Single(program.Collections);
        Assert.Equal(name, binding.Name);
        Assert.Equal(elementKind, binding.ElementKind);
    }

    [Fact]
    public void Represents_a_filter_predicate_over_the_item_name()
    {
        var requirement = new FilterRequirement(
            "large",
            "score",
            new SemanticNameExpression("scores"),
            new SemanticBinaryExpression(
                SemanticExpressionOperator.GreaterThan,
                new SemanticNameExpression("score"),
                new SemanticNumberExpression("2")));

        Assert.True(SemanticProgramValidator.IsValid(new SemanticProgram([requirement])));

        var withoutItem = new FilterRequirement(
            "large",
            "score",
            new SemanticNameExpression("scores"),
            new SemanticBinaryExpression(
                SemanticExpressionOperator.GreaterThan,
                new SemanticNameExpression("other"),
                new SemanticNumberExpression("2")));
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([withoutItem])));

        var sharedName = requirement with { ResultName = "score" };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([sharedName])));
    }

    [Fact]
    public void Represents_a_map_transformation_over_the_item_name()
    {
        var requirement = new MapRequirement(
            "doubled",
            "score",
            new SemanticNameExpression("scores"),
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Multiply,
                new SemanticNameExpression("score"),
                new SemanticNumberExpression("2")));

        Assert.True(SemanticProgramValidator.IsValid(new SemanticProgram([requirement])));

        var withoutItem = requirement with { Value = new SemanticNumberExpression("2") };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([withoutItem])));
    }

    [Fact]
    public void Represents_an_accumulator_with_its_initial_value_kind_and_dependent_update()
    {
        var requirement = new AccumulationRequirement(
            "total",
            SemanticCollectionItemKind.Number,
            "score",
            new SemanticNameExpression("scores"),
            new SemanticNumberExpression("0"),
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticNameExpression("total"),
                new SemanticNameExpression("score")));

        Assert.True(SemanticProgramValidator.IsValid(new SemanticProgram([requirement])));

        var withoutAccumulator = requirement with
        {
            Update = new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticNumberExpression("1"),
                new SemanticNameExpression("score"))
        };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([withoutAccumulator])));

        var withoutItem = requirement with
        {
            Update = new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticNameExpression("total"),
                new SemanticNumberExpression("1"))
        };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([withoutItem])));

        var invalidInitial = requirement with { InitialValue = new SemanticNameExpression(string.Empty) };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([invalidInitial])));

        var unknownKind = requirement with { AccumulatorKind = (SemanticCollectionItemKind) 99 };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([unknownKind])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Represents_a_sort_key_and_direction(bool descending)
    {
        var requirement = new SortRequirement(
            "ordered",
            "score",
            new SemanticNameExpression("scores"),
            new SemanticNameExpression("score"),
            descending);

        Assert.True(SemanticProgramValidator.IsValid(new SemanticProgram([requirement])));
        Assert.Equal(descending, requirement.Descending);

        var withoutItem = requirement with { Key = new SemanticNumberExpression("1") };
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([withoutItem])));
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
