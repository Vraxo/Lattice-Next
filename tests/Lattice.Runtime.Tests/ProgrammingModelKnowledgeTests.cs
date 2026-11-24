using Lattice.Core.Reasoning;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ProgrammingModelKnowledgeTests
{
    [Fact]
    public void Loads_core_program_goal_and_binding_types_with_typed_relations()
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
}
