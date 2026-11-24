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

        AssertRelation("program-goal-has-statement", "program-goal", "statement");
        AssertRelation("statement-has-expression", "statement", "expression");
        AssertRelation("assignment-targets-variable", "assignment-statement", "variable");
        AssertRelation("binding-binds-variable", "binding", "variable");
        AssertRelation("binding-supplies-value", "binding", "value");

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
