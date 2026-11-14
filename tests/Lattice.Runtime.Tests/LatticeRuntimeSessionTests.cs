using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class LatticeRuntimeSessionTests
{
    [Fact]
    public async Task Stores_assertions_and_answers_questions_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Fido is a dog. Is Fido a dog?");

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(
            LatticeUtteranceResultKind.StatementStored,
            result.Utterances[0].Kind);
        Assert.Equal(
            LatticeUtteranceResultKind.QuestionAnswered,
            result.Utterances[1].Kind);
        Assert.Equal(
            LatticeTruthValue.True,
            result.Utterances[1].TruthValue);
        Assert.Contains(
            result.Utterances[1].SupportingEvidence,
            evidence => evidence.Sources.Any(
                source => source.SourceText == "Fido is a dog."));
    }

    [Fact]
    public async Task Keeps_knowledge_isolated_between_sessions()
    {
        var client = new LatticeRuntimeClient();
        await using var first = await client.CreateSessionAsync();
        await using var second = await client.CreateSessionAsync();

        await first.SubmitAsync("Fido is a dog.");
        var result = await second.SubmitAsync("Is Fido a dog?");

        Assert.Equal(LatticeTruthValue.Unknown, result.Utterances[0].TruthValue);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task Requests_clarification_for_a_missing_referent()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("Is it a dog?");

        Assert.Equal(
            LatticeUtteranceResultKind.ClarificationRequired,
            result.Utterances[0].Kind);
        Assert.Equal(
            LatticeReferenceStatus.MissingReferent,
            result.Utterances[0].ReferenceStatus);
    }

    [Fact]
    public async Task Rejects_a_turn_before_mutating_session_knowledge_when_parsing_fails()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var rejected = await session.SubmitAsync(
            "Fido is a dog. asdf");
        var followUp = await session.SubmitAsync("Is Fido a dog?");

        Assert.Equal(LatticeTurnStatus.Rejected, rejected.Status);
        Assert.Equal(LatticeTruthValue.Unknown, followUp.Utterances[0].TruthValue);
    }

    [Fact]
    public async Task Rejects_submissions_after_session_disposal()
    {
        var client = new LatticeRuntimeClient();
        var session = await client.CreateSessionAsync();

        await session.DisposeAsync();
        var result = await session.SubmitAsync("Fido is a dog.");

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Equal("The session has been closed.", result.Error);
    }

    [Fact]
    public async Task Loads_knowledge_packs_into_the_session_with_imported_provenance()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var initialStatus = await session.GetStatusAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """{"formatVersion":1,"id":"animals","name":"Animals","claims":["Fido is a dog."]}""");
        var result = await session.SubmitAsync("Is Fido a dog?");
        var status = await session.GetStatusAsync();

        Assert.True(loaded.IsSuccess);
        Assert.Equal("Animals", loaded.Name);
        Assert.Equal(1, loaded.ClaimCount);
        Assert.Equal(LatticeTruthValue.True, result.Utterances[0].TruthValue);
        Assert.Contains(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(
                source => source.Kind == LatticeKnowledgeSourceKind.ImportedClaim &&
                    source.SourceId == "animals"));
        Assert.Equal(initialStatus.FactCount + 1, status.FactCount);
        Assert.Equal(initialStatus.RuleCount, status.RuleCount);
        Assert.Equal(initialStatus.LoadedKnowledgePackCount + 1, status.LoadedKnowledgePackCount);
    }

    [Fact]
    public async Task Answers_definition_questions_from_the_default_world_model_without_imports()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("What is a polygon?");

        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal(
            "A polygon is a plane figure that has a closed boundary.",
            result.Utterances[0].Message);
        Assert.Equal("ask(definition(polygon))", result.Utterances[0].Meaning);
        var definitionEvidence = Assert.Single(result.Utterances[0].SupportingEvidence);
        Assert.Contains(
            definitionEvidence.Sources,
            source => source.SourceId == "core-world-model:definition:polygon");
    }

    [Fact]
    public async Task Answers_verb_definition_from_compositional_default_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("What is a verb?");

        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal(
            "A verb is a part of speech that expresses an action or state.",
            result.Utterances[0].Message);
        Assert.Equal("ask(definition(verb))", result.Utterances[0].Meaning);
        var definitionEvidence = Assert.Single(result.Utterances[0].SupportingEvidence);
        Assert.Contains(
            definitionEvidence.Sources,
            source => source.SourceId == "core-world-model:definition:verb");
    }


    [Fact]
    public async Task Loads_only_the_typed_semantic_model_as_foundational_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var status = await session.GetStatusAsync();

        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
        Assert.Equal(0, status.LoadedKnowledgePackCount);
        Assert.Equal(197, status.SemanticConceptCount);
        Assert.Equal(38, status.SemanticRelationCount);
        Assert.Equal(0, status.SemanticDataAssertionCount);

        var tope = await session.SubmitAsync("Is Tope a fish?");
        var answer = Assert.Single(tope.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(LatticeTruthValue.True, answer.TruthValue);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:tope:instance-of:fish"));
    }

    [Fact]
    public async Task Answers_geometry_and_language_questions_from_typed_structures()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var polygon = await session.SubmitAsync("What is a polygon?");
        var boundary = await session.SubmitAsync("What is a boundary?");
        var segment = await session.SubmitAsync("What is a line segment?");
        var verb = await session.SubmitAsync("What is a verb?");

        Assert.Equal(
            "A polygon is a plane figure that has a closed boundary.",
            Assert.Single(polygon.Utterances).Message);
        Assert.Equal(
            "A boundary is a geometric structure that contains a line segment.",
            Assert.Single(boundary.Utterances).Message);
        Assert.Equal(
            "A line segment is a geometric element that has a geometric point as its start point, has a geometric point as its end point, and has straightness.",
            Assert.Single(segment.Utterances).Message);
        Assert.Equal(
            "A verb is a part of speech that expresses an action or state.",
            Assert.Single(verb.Utterances).Message);

        Assert.All(
            new[] { polygon, boundary, segment, verb },
            turn => Assert.Equal(
                LatticeUtteranceResultKind.QuestionAnswered,
                Assert.Single(turn.Utterances).Kind));
        Assert.Contains(
            polygon.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:definition:polygon"));
    }

    [Fact]
    public async Task Answers_monopsony_questions_from_structured_market_and_mechanism_relations()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var monopsony = await session.SubmitAsync("What is a monopsony?");
        var power = await session.SubmitAsync("What is monopsony power?");
        var causes = await session.SubmitAsync("What causes monopsony power?");

        Assert.Equal(
            "A monopsony is a buyer market structure that has a dominant buyer.",
            Assert.Single(monopsony.Utterances).Message);
        Assert.Equal(
            "Monopsony power is a wage-setting power that has a wage markdown.",
            Assert.Single(power.Utterances).Message);
        var causesMessage = Assert.Single(causes.Utterances).Message;
        Assert.StartsWith(
            "Monopsony power can arise through employer concentration, job differentiation, and search frictions.",
            causesMessage);
        Assert.Contains("Manning (2021), Monopsony in Labor Markets: A Review", causesMessage);
        Assert.Contains("Yeh, Macaluso, and Hershbein (2022), Monopsony in the US Labor Market", causesMessage);
        Assert.All(
            new[] { monopsony, power, causes },
            turn => Assert.Equal(
                LatticeUtteranceResultKind.QuestionAnswered,
                Assert.Single(turn.Utterances).Kind));
        Assert.Contains(
            causes.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:labor-market-monopsony-power:has-research-reference:azar-marinescu-2024-publication"));
    }

    [Fact]
    public async Task Describes_software_profiles_from_capability_relations_without_embedded_urls()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var idaes = await session.SubmitAsync("What is IDAES?");
        var biosteam = await session.SubmitAsync("What is BioSTEAM?");
        var pyomo = await session.SubmitAsync("What is Pyomo?");

        var idaesMessage = Assert.Single(idaes.Utterances).Message;
        var biosteamMessage = Assert.Single(biosteam.Utterances).Message;
        var pyomoMessage = Assert.Single(pyomo.Utterances).Message;

        Assert.Contains("process systems engineering framework", idaesMessage);
        Assert.Contains("equation-oriented process modeling", idaesMessage);
        Assert.Contains("It is used for process systems engineering.", idaesMessage);
        Assert.Contains("biorefinery modeling framework", biosteamMessage);
        Assert.Contains("techno-economic analysis", biosteamMessage);
        Assert.Contains("optimization modeling framework", pyomoMessage);
        Assert.Contains("mixed-integer optimization", pyomoMessage);
        Assert.All(
            new[] { idaes, biosteam, pyomo },
            turn => Assert.Equal(
                LatticeUtteranceResultKind.QuestionAnswered,
                Assert.Single(turn.Utterances).Kind));
        Assert.All(
            new[] { idaesMessage, biosteamMessage, pyomoMessage },
            message => Assert.DoesNotContain("https://", message, StringComparison.Ordinal));
        Assert.Contains(
            idaes.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:idaes:instance-of:process-systems-engineering-framework"));
    }

    [Fact]
    public async Task Classifies_multiple_items_into_categories_using_default_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Identify which instrument is string or percussion: Cantaro, Gudok");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal(
            "Cantaro is a percussion instrument. Gudok is a string instrument.",
            utterance.Message);
        Assert.Equal(
            "ask(classify(instrument, categories(string, percussion), items(cantaro, gudok)))",
            utterance.Meaning);
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:cantaro:has-sound-production-mechanism:cantaro-mechanism"));
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:gudok:has-sound-production-mechanism:gudok-mechanism"));
    }

    [Fact]
    public async Task Classifies_Panduri_and_Zurna_using_default_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Identify which instrument is string or woodwind: Panduri, Zurna");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal(
            "Panduri is a string instrument. Zurna is a woodwind instrument.",
            utterance.Message);
        Assert.Equal(
            "ask(classify(instrument, categories(string, woodwind), items(panduri, zurna)))",
            utterance.Meaning);
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:panduri:has-sound-production-mechanism:panduri-mechanism"));
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:zurna:has-sound-production-mechanism:zurna-mechanism"));
    }


    [Fact]
    public async Task Categorizes_seven_instruments_into_string_and_keyboard_families()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Categorize each of the following instruments as either string or keyboard: Guitar, Violin, piano, harmonium, cello, accordion, banjo");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal(
            "Guitar is a string instrument. Violin is a string instrument. Piano is both a string and a keyboard instrument. Harmonium is a keyboard instrument. Cello is a string instrument. Accordion is a keyboard instrument. Banjo is a string instrument.",
            utterance.Message);
        Assert.Equal(
            "ask(classify(instrument, categories(string, keyboard), items(guitar, violin, piano, harmonium, cello, accordion, banjo)))",
            utterance.Meaning);
        Assert.Equal(7, utterance.SupportingEvidence.Length);
        foreach (var assertionId in new[]
        {
            "core-world-model:relation:guitar:has-sound-production-mechanism:guitar-mechanism",
            "core-world-model:relation:violin:has-sound-production-mechanism:violin-mechanism",
            "core-world-model:relation:piano:has-playing-interface:piano-keyboard",
            "core-world-model:relation:harmonium:has-playing-interface:harmonium-keyboard",
            "core-world-model:relation:cello:has-sound-production-mechanism:cello-mechanism",
            "core-world-model:relation:accordion:has-playing-interface:accordion-keyboard",
            "core-world-model:relation:banjo:has-sound-production-mechanism:banjo-mechanism"
        })
        {
            Assert.Contains(
                utterance.SupportingEvidence,
                evidence => evidence.Sources.Any(source => source.SourceId == assertionId));
        }

        var pianoSoundProduction = await session.SubmitAsync(
            "Is Piano a struck-string-instrument?");
        var pianoSoundProductionAnswer = Assert.Single(pianoSoundProduction.Utterances);
        Assert.Equal(LatticeTruthValue.True, pianoSoundProductionAnswer.TruthValue);
        Assert.Contains(
            pianoSoundProductionAnswer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:piano:has-sound-production-mechanism:piano-action"));
    }


    [Fact]
    public async Task Answers_instrument_questions_by_composing_typed_concept_definitions_and_relations()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var musicalInstrument = await session.SubmitAsync("What is a musical instrument?");
        Assert.Equal(
            "A musical instrument is a physical object that uses a sound-production mechanism.",
            Assert.Single(musicalInstrument.Utterances).Message);

        var keyboardDefinition = await session.SubmitAsync("What makes something a keyboard instrument?");
        Assert.Equal(
            "A keyboard instrument is a musical instrument that has a keyboard as its playing interface.",
            Assert.Single(keyboardDefinition.Utterances).Message);

        var keyboardReason = await session.SubmitAsync("Why is a piano a keyboard instrument?");
        Assert.Equal(
            "A piano is a keyboard instrument because it has a keyboard as its playing interface.",
            Assert.Single(keyboardReason.Utterances).Message);

        var stringReason = await session.SubmitAsync("Why is a piano a struck-string instrument?");
        Assert.Equal(
            "A piano is a struck-string instrument because it uses a struck-string mechanism.",
            Assert.Single(stringReason.Utterances).Message);

        var soundMechanism = await session.SubmitAsync("How does a piano produce sound?");
        Assert.Equal(
            "A piano uses the piano action. The piano action actuates the piano hammers. The piano hammers strike the piano strings. The piano strings transmit vibrations to the piano soundboard. The piano soundboard radiates the acoustic sound.",
            Assert.Single(soundMechanism.Utterances).Message);

        var comparison = await session.SubmitAsync("What distinguishes a piano from a harmonium?");
        Assert.Equal(
            "Both have a keyboard as their playing interface. Piano uses a struck-string mechanism, while harmonium uses a free-reed mechanism.",
            Assert.Single(comparison.Utterances).Message);

        Assert.All(
            new[] { musicalInstrument, keyboardDefinition, keyboardReason, stringReason, soundMechanism, comparison },
            turn => Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, Assert.Single(turn.Utterances).Kind));
        Assert.Contains(
            keyboardReason.Utterances[0].SupportingEvidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:piano:has-playing-interface:piano-keyboard"));
        Assert.Contains(
            soundMechanism.Utterances[0].SupportingEvidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:piano-hammers:strikes:piano-strings"));
    }

    [Fact]
    public async Task Does_not_classify_an_item_without_exactly_one_supported_category()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Identify which instrument is string or percussion: Zurna, Panduri");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.Empty(utterance.SupportingEvidence);
    }

    [Fact]
    public async Task Selects_the_only_choice_supported_by_default_category_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("Which is a species of fish? Tope or Rope");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal("Tope is a species of fish.", utterance.Message);
        Assert.Equal("ask(choice(fish, tope, rope))", utterance.Meaning);
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.Kind == LatticeKnowledgeSourceKind.FoundationalKnowledge &&
                source.SourceId == "core-world-model:relation:tope:instance-of:fish"));
    }

    [Fact]
    public async Task Does_not_guess_when_no_choice_has_supporting_category_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("Which is a species of mammal? Tope or Rope");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.Empty(utterance.SupportingEvidence);
    }

    [Fact]
    public async Task Realizes_a_different_concept_from_its_own_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """{"formatVersion":1,"id":"circles","name":"Circles","claims":["Circle is a geometric-shape."]}""");
        var result = await session.SubmitAsync("What is a circle?");

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal("A circle is a geometric shape.", result.Utterances[0].Message);
        Assert.Contains(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source => source.SourceId == "circles"));
    }

    [Fact]
    public async Task Does_not_fall_back_to_a_broader_concept_when_specific_knowledge_is_missing()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        await session.LoadKnowledgePackAsync(
            """
            {
              "formatVersion": 1,
              "id": "geometry-concepts",
              "name": "Geometry concepts",
              "claims": [
                "Polygon is a geometric-shape.",
                "Polygon has Boundary."
              ]
            }
            """);

        var result = await session.SubmitAsync("What is a regular polygon?");

        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, result.Utterances[0].Kind);
        Assert.Equal(
            "I don't have structured knowledge about regular polygon in the available knowledge.",
            result.Utterances[0].Message);
        Assert.Empty(result.Utterances[0].SupportingEvidence);
    }

    [Fact]
    public async Task Stops_concept_traversal_when_a_relationship_forms_a_cycle()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """
            {
              "formatVersion": 1,
              "id": "cyclic-concepts",
              "name": "Cyclic concepts",
              "claims": [
                "Loop has Boundary.",
                "Boundary has Loop."
              ]
            }
            """);

        var result = await session.SubmitAsync("What is a loop?");

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Contains(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source => source.SourceText == "Loop has Boundary."));
        Assert.Contains(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source => source.SourceText == "Boundary has Loop."));
        Assert.Contains("A loop has a boundary.", result.Utterances[0].Message);
        Assert.Contains("A boundary has a loop.", result.Utterances[0].Message);
        Assert.Equal(
            1,
            CountOccurrences(result.Utterances[0].Message, "A loop has a boundary."));
        Assert.Equal(
            1,
            CountOccurrences(result.Utterances[0].Message, "A boundary has a loop."));
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var offset = 0;

        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }

    [Fact]
    public async Task Rejects_knowledge_pack_loading_after_session_disposal()
    {
        var client = new LatticeRuntimeClient();
        var session = await client.CreateSessionAsync();

        await session.DisposeAsync();
        var result = await session.LoadKnowledgePackAsync("{}");

        Assert.False(result.IsSuccess);
        Assert.Equal("The session has been closed.", result.Error);
    }

    [Fact]
    public async Task Derives_free_reed_instruments_from_airflow_and_vibrating_reed_structure()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var harmonium = await session.SubmitAsync("What is harmonium?");
        var accordion = await session.SubmitAsync("What is accordion?");

        Assert.Equal(
            LatticeUtteranceResultKind.QuestionAnswered,
            Assert.Single(harmonium.Utterances).Kind);
        Assert.Contains("free-reed instrument", Assert.Single(harmonium.Utterances).Message);
        Assert.Contains("free-reed instrument", Assert.Single(accordion.Utterances).Message);

        var evidence = harmonium.Utterances[0].SupportingEvidence;
        Assert.Contains(
            evidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:definition:free-reed-component"));
        Assert.Contains(
            evidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:definition:free-reed-mechanism"));
        Assert.Contains(
            evidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:harmonium-reeds:receives-air-from:harmonium-bellows"));
        Assert.Contains(
            evidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:harmonium-reeds:vibrates-to-produce:harmonium-sound"));
        Assert.Contains(
            evidence,
            item => item.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:harmonium-action:directs-air-through:harmonium-reeds"));
    }

}
