using System.Text;
using Lattice.Core.Language;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class InputNormalizerTests
{
    [Fact]
    public void Normalizes_unicode_to_canonical_composition()
    {
        var result = InputNormalizer.Normalize("cafe\u0301");

        Assert.Equal("café", result);
        Assert.True(result.IsNormalized(NormalizationForm.FormC));
    }

    [Fact]
    public void Converts_nonbreaking_and_zero_width_spaces_to_word_separators()
    {
        var result = InputNormalizer.Normalize("Fido\u00A0is\u200Ba\u202Fdog.");

        Assert.Equal("Fido is a dog.", result);
    }

    [Fact]
    public void Removes_a_leading_bom_and_soft_hyphen_formatting()
    {
        var result = InputNormalizer.Normalize("\uFEFFFi\u00ADdo is a dog.");

        Assert.Equal("Fido is a dog.", result);
    }

    [Fact]
    public void Converts_an_embedded_bom_to_a_separator_instead_of_merging_tokens()
    {
        var result = InputNormalizer.Normalize("Fido\uFEFFis a dog.");

        Assert.Equal("Fido is a dog.", result);
    }

    [Fact]
    public void Preserves_meaningful_characters_and_does_not_correct_words()
    {
        var result = InputNormalizer.Normalize("Fido are a dgo.");

        Assert.Equal("Fido are a dgo.", result);
    }

    [Fact]
    public void Rejects_a_null_input_as_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => InputNormalizer.Normalize(null!));
    }
}
