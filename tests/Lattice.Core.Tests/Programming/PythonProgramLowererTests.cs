using System.Collections.Immutable;
using Lattice.Core.Programming;
using Xunit;

namespace Lattice.Core.Tests.Programming;

public sealed class PythonProgramLowererTests
{
    [Fact]
    public void Lowers_language_neutral_plan_to_python_without_task_specific_templates()
    {
        var plan = new ProgramPlan(ImmutableArray.Create<ProgramStatementPlan>(
            new AssignValuePlan(
                new VariablePlan("values"),
                new SequenceValuePlan(ImmutableArray.Create<ProgramExpressionPlan>(
                    new NumberValuePlan(2),
                    new NumberValuePlan(4),
                    new NumberValuePlan(6)))),
            new AssignValuePlan(new VariablePlan("total"), new NumberValuePlan(0)),
            new IterateValuesPlan(
                "value",
                new VariablePlan("values"),
                ImmutableArray.Create<ProgramStatementPlan>(
                    new ConditionalPlan(
                        new CombineValuesPlan(
                            new VariablePlan("value"),
                            ProgramBinaryOperator.GreaterThan,
                            new NumberValuePlan(2)),
                        ImmutableArray.Create<ProgramStatementPlan>(
                            new AssignValuePlan(
                                new VariablePlan("total"),
                                new CombineValuesPlan(
                                    new VariablePlan("total"),
                                    ProgramBinaryOperator.Add,
                                    new VariablePlan("value")))),
                        ImmutableArray<ProgramStatementPlan>.Empty))),
            new EvaluateExpressionPlan(
                new InvokeOperationPlan(
                    new VariablePlan("print"),
                    ImmutableArray.Create<ProgramExpressionPlan>(new VariablePlan("total")),
                    ImmutableArray<OperationArgumentPlan>.Empty))));

        var python = PythonProgramLowerer.Lower(plan);
        var source = PythonCodeRenderer.Render(python);

        Assert.Equal(
            "values = [2, 4, 6]" + Environment.NewLine +
            "total = 0" + Environment.NewLine +
            "for value in values:" + Environment.NewLine +
            "    if (value > 2):" + Environment.NewLine +
            "        total = (total + value)" + Environment.NewLine +
            "print(total)" + Environment.NewLine,
            source);
    }

    [Fact]
    public void Lowers_api_calls_and_member_access_as_composable_operations()
    {
        var plan = new ProgramPlan(ImmutableArray.Create<ProgramStatementPlan>(
            new ImportModulePlan("example_api"),
            new EvaluateExpressionPlan(
                new InvokeOperationPlan(
                    new MemberAccessPlan(
                        new VariablePlan("example_api"),
                        "create_item"),
                    ImmutableArray.Create<ProgramExpressionPlan>(
                        new TextValuePlan("sample")),
                    ImmutableArray.Create(
                        new OperationArgumentPlan("enabled", new BooleanValuePlan(true)))))));

        var source = PythonCodeRenderer.Render(PythonProgramLowerer.Lower(plan));

        Assert.Equal(
            "import example_api" + Environment.NewLine +
            "example_api.create_item(\"sample\", enabled=True)" + Environment.NewLine,
            source);
    }
}
