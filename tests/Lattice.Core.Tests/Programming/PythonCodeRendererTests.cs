using System.Collections.Immutable;
using Lattice.Core.Programming;
using Xunit;

namespace Lattice.Core.Tests.Programming;

public sealed class PythonCodeRendererTests
{
    [Fact]
    public void Renders_hello_world_from_general_program_nodes()
    {
        var program = new PythonProgram(ImmutableArray.Create<PythonStatement>(
            new PythonExpressionStatement(
                new PythonCallExpression(
                    new PythonNameExpression("print"),
                    ImmutableArray.Create<PythonExpression>(new PythonStringExpression("Hello, world!")),
                    ImmutableArray<PythonKeywordArgument>.Empty))));

        var source = PythonCodeRenderer.Render(program);

        Assert.Equal("print(\"Hello, world!\")" + Environment.NewLine, source);
    }

    [Fact]
    public void Renders_assignments_loops_conditions_and_calls_compositionally()
    {
        var program = new PythonProgram(ImmutableArray.Create<PythonStatement>(
            new PythonAssignmentStatement(
                new PythonNameExpression("numbers"),
                new PythonListExpression(ImmutableArray.Create<PythonExpression>(
                    new PythonNumberExpression(1),
                    new PythonNumberExpression(2),
                    new PythonNumberExpression(3)))),
            new PythonAssignmentStatement(
                new PythonNameExpression("total"),
                new PythonNumberExpression(0)),
            new PythonForStatement(
                new PythonNameExpression("number"),
                new PythonNameExpression("numbers"),
                ImmutableArray.Create<PythonStatement>(
                    new PythonIfStatement(
                        new PythonBinaryExpression(
                            new PythonNameExpression("number"),
                            PythonBinaryOperator.GreaterThan,
                            new PythonNumberExpression(1)),
                        ImmutableArray.Create<PythonStatement>(
                            new PythonAssignmentStatement(
                                new PythonNameExpression("total"),
                                new PythonBinaryExpression(
                                    new PythonNameExpression("total"),
                                    PythonBinaryOperator.Add,
                                    new PythonNameExpression("number")))),
                        ImmutableArray<PythonStatement>.Empty))),
            new PythonExpressionStatement(
                new PythonCallExpression(
                    new PythonNameExpression("print"),
                    ImmutableArray.Create<PythonExpression>(new PythonNameExpression("total")),
                    ImmutableArray<PythonKeywordArgument>.Empty))));

        var source = PythonCodeRenderer.Render(program);

        Assert.Equal(
            "numbers = [1, 2, 3]" + Environment.NewLine +
            "total = 0" + Environment.NewLine +
            "for number in numbers:" + Environment.NewLine +
            "    if (number > 1):" + Environment.NewLine +
            "        total = (total + number)" + Environment.NewLine +
            "print(total)" + Environment.NewLine,
            source);
    }

    [Fact]
    public void Renders_functions_imports_and_dictionary_literals()
    {
        var program = new PythonProgram(ImmutableArray.Create<PythonStatement>(
            new PythonImportStatement("math"),
            new PythonFunctionDefinitionStatement(
                "describe",
                ImmutableArray.Create("item"),
                ImmutableArray.Create<PythonStatement>(
                    new PythonReturnStatement(
                        new PythonCallExpression(
                            new PythonNameExpression("str"),
                            ImmutableArray.Create<PythonExpression>(
                                new PythonAttributeExpression(
                                    new PythonNameExpression("item"),
                                    "name")),
                            ImmutableArray<PythonKeywordArgument>.Empty)))),
            new PythonAssignmentStatement(
                new PythonNameExpression("record"),
                new PythonDictionaryExpression(ImmutableArray.Create(
                    new PythonDictionaryEntry(
                        new PythonStringExpression("name"),
                        new PythonStringExpression("sample")))))));

        var source = PythonCodeRenderer.Render(program);

        Assert.Equal(
            "import math" + Environment.NewLine +
            "def describe(item):" + Environment.NewLine +
            "    return str(item.name)" + Environment.NewLine +
            "record = {\"name\": \"sample\"}" + Environment.NewLine,
            source);
    }

    [Fact]
    public void Rejects_invalid_identifiers_instead_of_emitting_invalid_source()
    {
        var program = new PythonProgram(ImmutableArray.Create<PythonStatement>(
            new PythonAssignmentStatement(
                new PythonNameExpression("not-valid"),
                new PythonNumberExpression(1))));

        Assert.Throws<ArgumentException>(() => PythonCodeRenderer.Render(program));
    }
}
