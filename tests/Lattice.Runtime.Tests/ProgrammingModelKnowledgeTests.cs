using Lattice.Core.Reasoning;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ProgrammingModelKnowledgeTests
{
    [Fact]
    public void Loads_core_programming_types_and_operation_contract_signatures()
    {
        var model = SemanticWorldModelResourceLoader.Load();

        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("program-goal")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("binding")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("statement")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("expression")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("variable")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("value")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("programming-operation")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("operation-input")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("operation-result")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("programming-effect")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("programming-capability")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("operation-constraint")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("function-definition")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("function-call")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("parameter")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("argument")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("conditional-statement")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("loop-statement")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("module")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("exception")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("exception-handler")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("input")?.Kind);
        Assert.Equal(SemanticConceptKind.Type, model.FindConcept("output")?.Kind);

        AssertRelation("program-goal-has-statement", "program-goal", "statement");
        AssertRelation("statement-has-expression", "statement", "expression");
        AssertRelation("assignment-targets-variable", "assignment-statement", "variable");
        AssertRelation("binding-binds-variable", "binding", "variable");
        AssertRelation("binding-supplies-value", "binding", "value");
        AssertRelation("operation-has-input", "programming-operation", "operation-input");
        AssertRelation("operation-has-result", "programming-operation", "operation-result");
        AssertRelation("operation-has-effect", "programming-operation", "programming-effect");
        AssertRelation("operation-requires-capability", "programming-operation", "programming-capability");
        AssertRelation("operation-has-constraint", "programming-operation", "operation-constraint");
        AssertRelation("operation-requires-operation", "programming-operation", "programming-operation");
        AssertRelation("operation-input-receives-value", "operation-input", "value");
        AssertRelation("operation-input-reads-variable", "operation-input", "variable");
        AssertRelation("operation-result-produces-value", "operation-result", "value");
        AssertRelation("operation-result-writes-variable", "operation-result", "variable");
        AssertRelation("function-has-parameter", "function", "parameter");
        AssertRelation("call-passes-argument", "function-call", "argument");
        var invokesCallable = Assert.IsType<SemanticRelationDefinition>(model.FindRelation("call-invokes-callable"));
        Assert.Null(invokesCallable.DomainType);
        Assert.Null(invokesCallable.RangeType);
        Assert.Equal(SemanticRelationArgumentKind.Type, invokesCallable.SubjectKind);
        Assert.Equal(SemanticRelationArgumentKind.Type, invokesCallable.ObjectKind);
        AssertRelation("function-returns-value", "function", "value");
        AssertRelation("function-definition-defines-function", "function-definition", "function");
        AssertRelation("function-definition-has-body-statement", "function-definition", "statement");
        AssertRelation("call-argument-binds-parameter", "argument", "parameter");
        AssertRelation("conditional-has-condition", "conditional-statement", "expression");
        AssertRelation("conditional-has-then-statement", "conditional-statement", "statement");
        AssertRelation("conditional-has-else-statement", "conditional-statement", "statement");
        AssertRelation("loop-has-iterable", "loop-statement", "expression");
        AssertRelation("loop-binds-variable", "loop-statement", "variable");
        AssertRelation("loop-has-body-statement", "loop-statement", "statement");
        AssertRelation("statement-depends-on-statement", "statement", "statement");
        AssertRelation("import-brings-module-into-scope", "import-statement", "module");
        AssertRelation("handler-handles-exception", "exception-handler", "exception");
        AssertRelation("statement-may-raise-exception", "statement", "exception");
        AssertRelation("exception-handler-handles-statement", "exception-handler", "statement");
        AssertRelation("import-targets-module", "import-statement", "module");
        AssertRelation("program-receives-input", "program", "input");
        AssertRelation("program-produces-output", "program", "output");
        var printOutput = Assert.IsType<SemanticRelationDefinition>(model.FindRelation("print-produces-output"));
        Assert.Equal("built-in-function", printOutput.DomainType);
        Assert.Null(printOutput.RangeType);
        Assert.Equal(SemanticRelationArgumentKind.Type, printOutput.ObjectKind);
        Assert.Equal(SemanticDataValueKind.Number, model.FindDataProperty("function-parameter-position")?.ValueKind);
        Assert.Equal("parameter", model.FindDataProperty("function-parameter-position")?.DomainType);
        Assert.Equal(SemanticDataValueKind.Number, model.FindDataProperty("call-argument-position")?.ValueKind);
        Assert.Equal("argument", model.FindDataProperty("call-argument-position")?.DomainType);
        Assert.Equal(SemanticDataValueKind.Number, model.FindDataProperty("statement-sequence-position")?.ValueKind);

        var inputType = Assert.IsType<SemanticRelationDefinition>(model.FindRelation("operation-input-requires-type"));
        Assert.Equal("operation-input", inputType.DomainType);
        Assert.Null(inputType.RangeType);
        Assert.Equal(SemanticRelationArgumentKind.Type, inputType.ObjectKind);
        var resultType = Assert.IsType<SemanticRelationDefinition>(model.FindRelation("operation-result-has-type"));
        Assert.Equal("operation-result", resultType.DomainType);
        Assert.Null(resultType.RangeType);
        Assert.Equal(SemanticRelationArgumentKind.Type, resultType.ObjectKind);
        Assert.Equal(SemanticDataValueKind.Number, model.FindDataProperty("operation-input-position")?.ValueKind);
        Assert.Equal(SemanticDataValueKind.Number, model.FindDataProperty("operation-result-position")?.ValueKind);
        Assert.Equal("operation-input", model.FindDataProperty("operation-input-position")?.DomainType);
        Assert.Equal("operation-result", model.FindDataProperty("operation-result-position")?.DomainType);

        void AssertRelation(string id, string domain, string range)
        {
            var relation = Assert.IsType<SemanticRelationDefinition>(model.FindRelation(id));
            Assert.Equal(domain, relation.DomainType);
            Assert.Equal(range, relation.RangeType);
            Assert.Equal(SemanticRelationArgumentKind.Individual, relation.SubjectKind);
            Assert.Equal(SemanticRelationArgumentKind.Individual, relation.ObjectKind);
        }
    }

    [Fact]
    public void Reasoner_keeps_shared_type_taxonomy_language_neutral()
    {
        var reasoner = new SemanticWorldModelReasoner(SemanticWorldModelResourceLoader.Load());

        Assert.True(reasoner.IsSubtypeOf("function", "callable"));
        Assert.True(reasoner.IsInstanceOf("python", "programming-language"));
        Assert.True(reasoner.IsSubtypeOf("conditional-statement", "statement"));
        Assert.True(reasoner.IsSubtypeOf("loop-statement", "statement"));
    }
}
