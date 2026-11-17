using System.Collections.Immutable;
using System.Globalization;

namespace Lattice.Core.Reasoning;

internal static class SemanticWorldModelDataLoader
{
    internal static bool IsValidDataValue(
        SemanticDataPropertyDefinition property,
        SemanticWorldModelLoader.DataAssertionDocument assertion,
        out string normalizedValue,
        out string? normalizedUnit)
    {
        normalizedValue = string.Empty;
        normalizedUnit = null;
        if (property.ValueKind == SemanticDataValueKind.Uri)
        {
            if (assertion.Unit is not null ||
                !Uri.TryCreate(assertion.Value, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            normalizedValue = uri.AbsoluteUri;
            return true;
        }

        if (!decimal.TryParse(
            assertion.Value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value))
        {
            return false;
        }

        if (property.ValueKind == SemanticDataValueKind.Number)
        {
            if (assertion.Unit is not null)
            {
                return false;
            }

            normalizedValue = value.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (property.ValueKind != SemanticDataValueKind.Decimal ||
            !SemanticUnit.TryConvertToBase(value, assertion.Unit, out var dimension, out var baseValue) ||
            dimension != property.Dimension)
        {
            return false;
        }

        normalizedValue = baseValue.ToString(CultureInfo.InvariantCulture);
        normalizedUnit = SemanticUnit.GetBaseUnit(dimension);
        return true;
    }

    internal static bool TryCreateDataAssertions(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        ImmutableArray<SemanticDataPropertyDefinition> dataProperties,
        out ImmutableArray<SemanticDataAssertion> dataAssertions,
        out string? error)
    {
        dataAssertions = ImmutableArray<SemanticDataAssertion>.Empty;
        error = null;
        var properties = dataProperties.ToDictionary(property => property.Id, StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<SemanticDataAssertion>(document.DataAssertions.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.DataAssertions)
        {
            if (item is null ||
                !SemanticWorldModelValidation.IsIndividual(concepts, item.Subject) ||
                !properties.TryGetValue(item.Property, out var property) ||
                !IsValidDataValue(property, item, out var normalizedValue, out var normalizedUnit) ||
                !keys.Add($"{item.Subject}:{item.Property}:{normalizedValue}:{normalizedUnit}"))
            {
                error = "Every semantic data assertion must supply a unique value matching its declared type and a valid subject/property pair.";
                return false;
            }

            var sourceId = property.ValueKind == SemanticDataValueKind.Decimal
                ? $"{document.Id}:data:{item.Subject}:{item.Property}:{normalizedValue}:{normalizedUnit}"
                : $"{document.Id}:data:{item.Subject}:{item.Property}";
            builder.Add(new SemanticDataAssertion(
                item.Subject,
                item.Property,
                normalizedValue,
                new SemanticWorldModelSource(sourceId),
                normalizedUnit));
        }

        dataAssertions = builder.ToImmutable();
        return true;
    }

    internal static bool TryCreateDataProperties(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        out ImmutableArray<SemanticDataPropertyDefinition> dataProperties,
        out string? error)
    {
        dataProperties = ImmutableArray<SemanticDataPropertyDefinition>.Empty;
        error = null;
        var builder = ImmutableArray.CreateBuilder<SemanticDataPropertyDefinition>(document.DataProperties.Count);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.DataProperties)
        {
            if (item is null ||
                !SemanticWorldModelValidation.IsValidId(item.Id) ||
                !SemanticWorldModelValidation.IsType(concepts, item.DomainType) ||
                !Enum.TryParse<SemanticDataValueKind>(item.ValueType, true, out var valueKind) ||
                !identifiers.Add(item.Id))
            {
                error = "Every data property requires a unique id, a declared domain type, and a supported typed value kind.";
                return false;
            }

            string? dimension = null;
            if (valueKind == SemanticDataValueKind.Decimal)
            {
                if (document.FormatVersion < 4)
                {
                    error = $"Decimal data property '{item.Id}' requires semantic world model version 4.";
                    return false;
                }

                if (!SemanticUnit.TryGetDimension(item.DimensionUnit, out var resolvedDimension))
                {
                    error = $"Decimal data property '{item.Id}' requires a supported dimensionUnit.";
                    return false;
                }

                dimension = resolvedDimension;
            }
            else if (item.DimensionUnit is not null)
            {
                error = $"Only decimal data properties may declare a dimensionUnit ('{item.Id}').";
                return false;
            }

            builder.Add(new SemanticDataPropertyDefinition(item.Id, item.DomainType, valueKind, dimension));
        }

        dataProperties = builder.ToImmutable();
        return true;
    }
}
