using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

// Temporary probe: always fails and reports every stage of the knowledge generation path.
public sealed class GenerationPathProbeTests
{
    [Theory]
    [InlineData("write a hello world program in python")]
    [InlineData("Create a Python program that prints \"Hello, world!\".")]
    [InlineData("Create a Python program that, from a list of numbers, builds a dictionary named squares that maps each number to the square of it.")]
    public void Probe(string input)
    {
        var report = new StringBuilder();

        var knowledgeOk = SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var knowledgeError);
        report.AppendLine($"knowledge={knowledgeOk} err={knowledgeError}");

        var loader = new GenerationLanguagePackLoader();
        var packsOk = loader.TryLoadEmbedded(out var packs, out var packError);
        report.AppendLine($"packs={packsOk} err={packError}");

        if (!knowledgeOk || !packsOk)
        {
            Assert.Fail($"PROBE[{input}]\n{report}");
        }

        var extractOk = SequenceInputFactExtractor.TryExtract(
            knowledge!, input, out var facts, out var extractError);
        var ids = facts.IsDefaultOrEmpty
            ? "<none>"
            : string.Join(", ", facts.Select(fact => fact.Id));
        report.AppendLine($"extract={extractOk} err={extractError}");
        report.AppendLine($"facts={ids}");

        var python = packs.TryGetValue("python", out var pack) ? pack : null;
        report.AppendLine($"pythonPack={python is not null}");

        if (python is not null)
        {
            var planOk = SemanticProgramPlanner.TryGenerateProgram(
                facts, python, out var code, out _, out var planError);
            report.AppendLine($"plan={planOk} err={planError}");
            if (planOk)
            {
                report.AppendLine($"code={code}");
            }

            var request = new KnowledgeDrivenSequenceGenerationRequest(
                ImmutableArray<string>.Empty,
                facts.IsDefaultOrEmpty
                    ? ImmutableArray<string>.Empty
                    : facts.Select(fact => fact.Id).ToImmutableArray())
            {
                FactValues = facts,
                LanguagePack = python
            };
            var sequenceOk = KnowledgeDrivenSequenceGenerator.TryGenerate(
                knowledge!, null, request, out var generated, out var sequenceError);
            report.AppendLine($"sequence={sequenceOk} err={sequenceError}");
            if (sequenceOk)
            {
                report.AppendLine($"text={generated!.Text}");
            }
        }

        Assert.Fail($"PROBE[{input}]\n{report}");
    }
}
