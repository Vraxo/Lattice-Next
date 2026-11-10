using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class NativeEnglishGrammarTests
{
    private readonly LceParser parser = new();

    [Theory]
    [InlineData("Fido belongs to the class of dogs.", UtteranceKind.Assertion, "dog(fido)")]
    [InlineData("All dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(dog(x), animal(x)))")]
    [InlineData("There is a red ball that is shiny.", UtteranceKind.Assertion, "exists(x, and(red(x), ball(x), shiny(x)))")]
    [InlineData("Milk is something that Fido likes.", UtteranceKind.Assertion, "likes(fido, milk)")]
    [InlineData("All dogs like milk.", UtteranceKind.Assertion, "forall(x, implies(dog(x), likes(x, milk)))")]
    [InlineData("Every dog that is red likes milk.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), likes(x, milk)))")]
    [InlineData("The ball that is red is shiny.", UtteranceKind.Assertion, "shiny(red-ball)")]
    [InlineData("Fido likes the ball that is red.", UtteranceKind.Assertion, "likes(fido, red-ball)")]
    [InlineData("Milk is liked by it.", UtteranceKind.Assertion, "likes(it, milk)")]
    [InlineData("Does Fido count as an animal?", UtteranceKind.Question, "ask(animal(fido))")]
    [InlineData("Is there a red ball?", UtteranceKind.Question, "ask(exists(x, and(red(x), ball(x))))")]
    [InlineData("Is milk liked by Fido?", UtteranceKind.Question, "ask(likes(fido, milk))")]
    [InlineData("Only dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(animal(x), dog(x)))")]
    [InlineData("All red dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), animal(x)))")]
    [InlineData("Every dog that is red is an animal.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), animal(x)))")]
    [InlineData("All dogs that are red are animals.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), animal(x)))")]
    [InlineData("All red dogs that are old like milk.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), old(x), dog(x)), likes(x, milk)))")]
    [InlineData("Some red dogs are animals.", UtteranceKind.Assertion, "exists(x, and(red(x), dog(x), animal(x)))")]
    [InlineData("Some dogs are shiny.", UtteranceKind.Assertion, "exists(x, and(dog(x), shiny(x)))")]
    [InlineData("There is a ball that is red that is shiny.", UtteranceKind.Assertion, "exists(x, and(ball(x), red(x), shiny(x)))")]
    [InlineData("The ball that is red that is shiny is a toy.", UtteranceKind.Assertion, "toy(red-shiny-ball)")]
    [InlineData("The ball that is red likes milk.", UtteranceKind.Assertion, "likes(red-ball, milk)")]
    [InlineData("Fido likes the ball that is red that is shiny.", UtteranceKind.Assertion, "likes(fido, red-shiny-ball)")]
    [InlineData("Does the ball that is red like milk?", UtteranceKind.Question, "ask(likes(red-ball, milk))")]
    [InlineData("Is the ball that is red shiny?", UtteranceKind.Question, "ask(shiny(red-ball))")]
    [InlineData("Milk is liked by Fido.", UtteranceKind.Assertion, "likes(fido, milk)") ]
    [InlineData("All red and shiny dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), shiny(x), dog(x)), animal(x)))")]
    [InlineData("Every dog that is red and shiny is an animal.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), shiny(x), dog(x)), animal(x)))")]
    [InlineData("All dogs that are red and shiny are animals.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), shiny(x), dog(x)), animal(x)))")]
    [InlineData("All red and shiny dogs that are old and tall like milk.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), shiny(x), old(x), tall(x), dog(x)), likes(x, milk)))")]
    [InlineData("Some red and shiny dogs are animals.", UtteranceKind.Assertion, "exists(x, and(red(x), shiny(x), dog(x), animal(x)))")]
    [InlineData("There is a red and shiny ball that is old and round.", UtteranceKind.Assertion, "exists(x, and(red(x), shiny(x), ball(x), old(x), round(x)))")]
    [InlineData("The red and shiny ball is a toy.", UtteranceKind.Assertion, "toy(red-shiny-ball)")]
    [InlineData("Fido likes the red and shiny ball.", UtteranceKind.Assertion, "likes(fido, red-shiny-ball)")]
    [InlineData("Does the red and shiny ball like milk?", UtteranceKind.Question, "ask(likes(red-shiny-ball, milk))")]
    [InlineData("All boxes are containers.", UtteranceKind.Assertion, "forall(x, implies(box(x), container(x)))")]
    [InlineData("All stories are books.", UtteranceKind.Assertion, "forall(x, implies(story(x), book(x)))")]
    [InlineData("All children are people.", UtteranceKind.Assertion, "forall(x, implies(child(x), person(x)))")]
    [InlineData("Some mice are animals.", UtteranceKind.Assertion, "exists(x, and(mouse(x), animal(x)))")]
    [InlineData("Fido belongs to the class of geese.", UtteranceKind.Assertion, "goose(fido)")]
    [InlineData("All knives are tools.", UtteranceKind.Assertion, "forall(x, implies(knife(x), tool(x)))")]
    [InlineData("All sheep are animals.", UtteranceKind.Assertion, "forall(x, implies(sheep(x), animal(x)))")]
    [InlineData("Every sheep is an animal.", UtteranceKind.Assertion, "forall(x, implies(sheep(x), animal(x)))")]
    [InlineData("All species are organisms.", UtteranceKind.Assertion, "forall(x, implies(species(x), organism(x)))")]
    [InlineData("Every species is an organism.", UtteranceKind.Assertion, "forall(x, implies(species(x), organism(x)))")]
    [InlineData("All deer are animals.", UtteranceKind.Assertion, "forall(x, implies(deer(x), animal(x)))")]
    [InlineData("Every deer is an animal.", UtteranceKind.Assertion, "forall(x, implies(deer(x), animal(x)))")]
    [InlineData("All fish are animals.", UtteranceKind.Assertion, "forall(x, implies(fish(x), animal(x)))")]
    [InlineData("Every fish is an animal.", UtteranceKind.Assertion, "forall(x, implies(fish(x), animal(x)))")]
    [InlineData("All series are collections.", UtteranceKind.Assertion, "forall(x, implies(series(x), collection(x)))")]
    [InlineData("Every series is a collection.", UtteranceKind.Assertion, "forall(x, implies(series(x), collection(x)))")]
    [InlineData("The species is rare.", UtteranceKind.Assertion, "rare(species)")]
    [InlineData("All analyses are reports.", UtteranceKind.Assertion, "forall(x, implies(analysis(x), report(x)))")]
    [InlineData("Every analysis is a report.", UtteranceKind.Assertion, "forall(x, implies(analysis(x), report(x)))")]
    [InlineData("All buses are vehicles.", UtteranceKind.Assertion, "forall(x, implies(bus(x), vehicle(x)))")]
    [InlineData("Every bus is a vehicle.", UtteranceKind.Assertion, "forall(x, implies(bus(x), vehicle(x)))")]
    [InlineData("All viruses are pathogens.", UtteranceKind.Assertion, "forall(x, implies(virus(x), pathogen(x)))")]
    [InlineData("Every virus is a pathogen.", UtteranceKind.Assertion, "forall(x, implies(virus(x), pathogen(x)))")]
    [InlineData("Every lens is an object.", UtteranceKind.Assertion, "forall(x, implies(lens(x), object(x)))")]
    [InlineData("All feet are body-parts.", UtteranceKind.Assertion, "forall(x, implies(foot(x), body-part(x)))")]
    [InlineData("All leaves are plants.", UtteranceKind.Assertion, "forall(x, implies(leaf(x), plant(x)))")]
    [InlineData("All lice are parasites.", UtteranceKind.Assertion, "forall(x, implies(louse(x), parasite(x)))")]
    [InlineData("All lives are experiences.", UtteranceKind.Assertion, "forall(x, implies(life(x), experience(x)))")]
    [InlineData("All men are adults.", UtteranceKind.Assertion, "forall(x, implies(man(x), adult(x)))")]
    [InlineData("All oxen are animals.", UtteranceKind.Assertion, "forall(x, implies(ox(x), animal(x)))")]
    [InlineData("All teeth are bones.", UtteranceKind.Assertion, "forall(x, implies(tooth(x), bone(x)))")]
    [InlineData("All theses are documents.", UtteranceKind.Assertion, "forall(x, implies(thesis(x), document(x)))")]
    [InlineData("All thieves are criminals.", UtteranceKind.Assertion, "forall(x, implies(thief(x), criminal(x)))")]
    [InlineData("All wives are spouses.", UtteranceKind.Assertion, "forall(x, implies(wife(x), spouse(x)))")]
    [InlineData("All women are adults.", UtteranceKind.Assertion, "forall(x, implies(woman(x), adult(x)))")]
    [InlineData("All wolves are animals.", UtteranceKind.Assertion, "forall(x, implies(wolf(x), animal(x)))")]
    [InlineData("All houses are buildings.", UtteranceKind.Assertion, "forall(x, implies(house(x), building(x)))")]
    [InlineData("All courses are lessons.", UtteranceKind.Assertion, "forall(x, implies(course(x), lesson(x)))")]
    [InlineData("All noses are body-parts.", UtteranceKind.Assertion, "forall(x, implies(nose(x), body-part(x)))")]
    [InlineData("All spouses are partners.", UtteranceKind.Assertion, "forall(x, implies(spouse(x), partner(x)))")]
    [InlineData("All algae are organisms.", UtteranceKind.Assertion, "forall(x, implies(alga(x), organism(x)))")]
    [InlineData("All appendices are documents.", UtteranceKind.Assertion, "forall(x, implies(appendix(x), document(x)))")]
    [InlineData("All antennae are sensors.", UtteranceKind.Assertion, "forall(x, implies(antenna(x), sensor(x)))")]
    [InlineData("All bacteria are organisms.", UtteranceKind.Assertion, "forall(x, implies(bacterium(x), organism(x)))")]
    [InlineData("All cacti are plants.", UtteranceKind.Assertion, "forall(x, implies(cactus(x), plant(x)))")]
    [InlineData("All criteria are requirements.", UtteranceKind.Assertion, "forall(x, implies(criterion(x), requirement(x)))")]
    [InlineData("All curricula are programs.", UtteranceKind.Assertion, "forall(x, implies(curriculum(x), program(x)))")]
    [InlineData("All fungi are organisms.", UtteranceKind.Assertion, "forall(x, implies(fungus(x), organism(x)))")]
    [InlineData("All formulae are expressions.", UtteranceKind.Assertion, "forall(x, implies(formula(x), expression(x)))")]
    [InlineData("All foci are points.", UtteranceKind.Assertion, "forall(x, implies(focus(x), point(x)))")]
    [InlineData("All indices are identifiers.", UtteranceKind.Assertion, "forall(x, implies(index(x), identifier(x)))")]
    [InlineData("All larvae are animals.", UtteranceKind.Assertion, "forall(x, implies(larva(x), animal(x)))")]
    [InlineData("All loci are positions.", UtteranceKind.Assertion, "forall(x, implies(locus(x), position(x)))")]
    [InlineData("All matrices are arrays.", UtteranceKind.Assertion, "forall(x, implies(matrix(x), array(x)))")]
    [InlineData("All nebulae are clouds.", UtteranceKind.Assertion, "forall(x, implies(nebula(x), cloud(x)))")]
    [InlineData("All nuclei are structures.", UtteranceKind.Assertion, "forall(x, implies(nucleus(x), structure(x)))")]
    [InlineData("All phenomena are events.", UtteranceKind.Assertion, "forall(x, implies(phenomenon(x), event(x)))")]
    [InlineData("All radii are measurements.", UtteranceKind.Assertion, "forall(x, implies(radius(x), measurement(x)))")]
    [InlineData("All syllabi are documents.", UtteranceKind.Assertion, "forall(x, implies(syllabus(x), document(x)))")]
    [InlineData("All stimuli are signals.", UtteranceKind.Assertion, "forall(x, implies(stimulus(x), signal(x)))")]
    [InlineData("All vertebrae are bones.", UtteranceKind.Assertion, "forall(x, implies(vertebra(x), bone(x)))")]
    [InlineData("All vertices are points.", UtteranceKind.Assertion, "forall(x, implies(vertex(x), point(x)))")]
    [InlineData("Every cactus is a plant.", UtteranceKind.Assertion, "forall(x, implies(cactus(x), plant(x)))")]
    [InlineData("Every focus is a point.", UtteranceKind.Assertion, "forall(x, implies(focus(x), point(x)))")]
    [InlineData("Every fungus is an organism.", UtteranceKind.Assertion, "forall(x, implies(fungus(x), organism(x)))")]
    [InlineData("Every locus is a position.", UtteranceKind.Assertion, "forall(x, implies(locus(x), position(x)))")]
    [InlineData("Every nucleus is a structure.", UtteranceKind.Assertion, "forall(x, implies(nucleus(x), structure(x)))")]
    [InlineData("Every radius is a measurement.", UtteranceKind.Assertion, "forall(x, implies(radius(x), measurement(x)))")]
    [InlineData("Every stimulus is a signal.", UtteranceKind.Assertion, "forall(x, implies(stimulus(x), signal(x)))")]
    [InlineData("Every syllabus is a document.", UtteranceKind.Assertion, "forall(x, implies(syllabus(x), document(x)))")]
    public void Parses_supported_english_directly_into_semantics(
        string input,
        UtteranceKind expectedKind,
        string expectedMeaning)
    {
        var result = parser.Parse(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(expectedKind, utterance.Kind);
        Assert.Equal(input, utterance.SourceText);
        Assert.Equal(expectedMeaning, SemanticFormatter.Format(utterance));
    }

    [Theory]
    [InlineData("No dog is a cat.")]
    [InlineData("Only red dogs are animals.")]
    [InlineData("Some dogs like milk.")]
    [InlineData("Fido does not like milk.")]
    [InlineData("Not every dog likes milk.")]
    [InlineData("Every dog likes some cat.")]
    [InlineData("Every dog that likes milk is friendly.")]
    [InlineData("Mira gave Fido a book.")]
    [InlineData("Could Fido be a dog?")]
    [InlineData("Fido might be a dog.")]
    [InlineData("Does Fido be happy?")]
    [InlineData("I saw her duck.")]
    [InlineData("All dogs that are red shiny are animals.")]
    [InlineData("All dogs that are red and are shiny are animals.")]
    [InlineData("All dogs that are red and are animals.")]
    [InlineData("All red and dogs are animals.")]
    [InlineData("Every children are animals.")]
    [InlineData("All child are animals.")]
    [InlineData("All analysis are reports.")]
    [InlineData("All bus are vehicles.")]
    [InlineData("All cactus are plants.")]
    [InlineData("All fungus are organisms.")]
    [InlineData("All syllabus are documents.")]
    [InlineData("All criterion are requirements.")]
    public void Does_not_guess_at_unsupported_or_ambiguous_meanings(string input)
    {
        var result = parser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
    }

    [Fact]
    public void Parses_mixed_native_english_and_lce_sentences_in_order()
    {
        var result = parser.ParseText(
            "All dogs are animals. Fido is a dog. Is Fido an animal?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Utterances.Length);
        Assert.Equal(
            "forall(x, implies(dog(x), animal(x)))",
            SemanticFormatter.Format(result.Utterances[0]));
        Assert.Equal("dog(fido)", SemanticFormatter.Format(result.Utterances[1]));
        Assert.Equal("ask(animal(fido))", SemanticFormatter.Format(result.Utterances[2]));
    }
}
