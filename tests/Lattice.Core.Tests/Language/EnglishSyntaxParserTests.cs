using Lattice.Core.Language;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class EnglishSyntaxParserTests
{
    [Fact]
    public void Parses_an_imperative_clause_with_an_object_and_prepositional_phrase()
    {
        const string input = "Write a Hello World program in Python.";

        Assert.True(EnglishSyntaxParser.TryParse(input, out var syntax));

        var clause = Assert.Single(syntax!.Clauses);
        Assert.Equal(EnglishClauseKind.Main, clause.Kind);
        Assert.Equal("write", clause.Verb);
        Assert.Null(clause.Subject);
        Assert.Equal("program", clause.Object!.Head);
        Assert.Contains("Hello", clause.Object.Modifiers);
        Assert.Contains("World", clause.Object.Modifiers);

        var phrase = Assert.Single(clause.PrepositionalPhrases);
        Assert.Equal("in", phrase.Preposition);
        Assert.Equal("Python", phrase.Object.Head);
    }

    [Fact]
    public void Parses_a_relative_clause_and_retains_its_object()
    {
        const string input = "Create a simple Python script that prints 'Hello, world!'.";

        Assert.True(EnglishSyntaxParser.TryParse(input, out var syntax));

        Assert.Equal(2, syntax!.Clauses.Length);
        var main = syntax.Clauses[0];
        var relative = syntax.Clauses[1];

        Assert.Equal("create", main.Verb);
        Assert.Equal("script", main.Object!.Head);
        Assert.Contains("Python", main.Object.Modifiers);
        Assert.Equal(EnglishClauseKind.Relative, relative.Kind);
        Assert.Equal("print", relative.Verb);
        Assert.Equal(EnglishSubjectBinding.Antecedent, relative.SubjectBinding);
        Assert.Equal("Hello, world!", relative.Object!.Text);
    }

    [Fact]
    public void Parses_an_infinitive_clause_without_confusing_it_with_a_prepositional_phrase()
    {
        const string input = "Generate a program in Python to display Hello, world.";

        Assert.True(EnglishSyntaxParser.TryParse(input, out var syntax));

        Assert.Equal(2, syntax!.Clauses.Length);
        Assert.Equal("generate", syntax.Clauses[0].Verb);
        Assert.Equal("Python", Assert.Single(syntax.Clauses[0].PrepositionalPhrases).Object.Head);
        Assert.Equal(EnglishClauseKind.Infinitive, syntax.Clauses[1].Kind);
        Assert.Equal("display", syntax.Clauses[1].Verb);
        Assert.Equal("Hello, world", syntax.Clauses[1].Object!.Text);
    }

    [Fact]
    public void Retains_an_explicit_subject_after_a_modal_auxiliary()
    {
        const string input = "Can you create a Python script?";

        Assert.True(EnglishSyntaxParser.TryParse(input, out var syntax));

        Assert.Equal("you", syntax!.Clauses[0].Subject!.Head);
        Assert.Equal("create", syntax.Clauses[0].Verb);
    }
    [Fact]
    public void Represents_an_omitted_subject_in_a_coordinated_clause_as_inherited()
    {
        const string input = "Fido likes milk and eats fish.";

        Assert.True(EnglishSyntaxParser.TryParse(input, out var syntax));

        Assert.Equal(2, syntax!.Clauses.Length);
        Assert.Equal("like", syntax.Clauses[0].Verb);
        Assert.Equal("fido", syntax.Clauses[0].Subject!.Head);
        Assert.Equal(EnglishClauseKind.Coordinated, syntax.Clauses[1].Kind);
        Assert.Equal("eat", syntax.Clauses[1].Verb);
        Assert.Equal(EnglishSubjectBinding.PreviousClause, syntax.Clauses[1].SubjectBinding);
        Assert.Equal("fish", syntax.Clauses[1].Object!.Head);
    }

    [Fact]
    public void Retains_an_explicit_subject_in_a_coordinated_clause()
    {
        const string input = "Fido likes milk and Rex eats fish.";

        Assert.True(EnglishSyntaxParser.TryParse(input, out var syntax));

        Assert.Equal(2, syntax!.Clauses.Length);
        Assert.Equal(EnglishClauseKind.Coordinated, syntax.Clauses[1].Kind);
        Assert.Equal(EnglishSubjectBinding.Explicit, syntax.Clauses[1].SubjectBinding);
        Assert.Equal("Rex", syntax.Clauses[1].Subject!.Head);
        Assert.Equal("fish", syntax.Clauses[1].Object!.Head);
    }

}
