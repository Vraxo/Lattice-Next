using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class LceTextParser
{
    internal static TextParseResult Parse(LceParser parser, string? input)
    {
        if (string.IsNullOrWhiteSpace(input) ||
            string.IsNullOrWhiteSpace(InputNormalizer.Normalize(input)))
        {
            return TextParseResult.Failure("Input is empty.");
        }

        var source = input.Trim();

        if (SemanticWorldModelQuestionParser.TryParse(source, out var semanticQuestion))
        {
            return TextParseResult.Success(ImmutableArray.Create(semanticQuestion!));
        }

        if (ChoiceQuestionParser.TryParse(source, out var choiceQuestionUtterance))
        {
            return TextParseResult.Success(ImmutableArray.Create(choiceQuestionUtterance!));
        }

        if (MultiItemClassificationParser.TryParse(source, out var classificationUtterance))
        {
            return TextParseResult.Success(ImmutableArray.Create(classificationUtterance!));
        }

        if (TaskRequestParser.TryParse(source, source, out var taskRequestUtterance))
        {
            return TextParseResult.Success(ImmutableArray.Create(taskRequestUtterance!));
        }

        if (LceObjectiveParser.TryParseObjective(source, source, out var objectiveUtterance))
        {
            return TextParseResult.Success(ImmutableArray.Create(objectiveUtterance!));
        }

        var utterances = ImmutableArray.CreateBuilder<SemanticUtterance>();
        var sentenceStart = 0;

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] is not ('.' or '?'))
            {
                continue;
            }

            var sentence = source[sentenceStart..(index + 1)].Trim();
            var result = parser.Parse(sentence);

            if (!result.IsSuccess || result.Utterance is null)
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count + 1}: {result.Error ?? "The sentence could not be parsed."}");
            }

            utterances.Add(result.Utterance);
            sentenceStart = index + 1;

            if (sentenceStart < source.Length &&
                !InputNormalizer.IsTextSeparator(source[sentenceStart]))
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count} must be separated from the next sentence by whitespace.");
            }

            while (sentenceStart < source.Length &&
                   InputNormalizer.IsTextSeparator(source[sentenceStart]))
            {
                sentenceStart++;
            }
        }

        if (sentenceStart < source.Length)
        {
            var finalSentence = source[sentenceStart..].Trim();
            var finalResult = parser.Parse(finalSentence);
            if (!finalResult.IsSuccess || finalResult.Utterance is null)
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count + 1} is incomplete. Every sentence must end with a period or question mark unless it is independently recognized.");
            }

            utterances.Add(finalResult.Utterance);
        }

        if (utterances.Count == 0)
        {
            return TextParseResult.Failure("Input does not contain a complete sentence.");
        }

        return TextParseResult.Success(utterances.ToImmutable());
    }
}
