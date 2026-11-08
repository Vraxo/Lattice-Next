namespace Lattice.Core.Semantics;

public static class SemanticTermComparer
{
    public static bool AreEquivalent(SemanticTerm left, SemanticTerm right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return (left, right) switch
        {
            (IndividualTerm leftIndividual, IndividualTerm rightIndividual) =>
                leftIndividual.Identifier == rightIndividual.Identifier,
            (DefiniteDescriptionTerm leftDescription, DefiniteDescriptionTerm rightDescription) =>
                leftDescription.Head == rightDescription.Head &&
                !leftDescription.Modifiers.IsDefault &&
                !rightDescription.Modifiers.IsDefault &&
                leftDescription.Modifiers.SequenceEqual(rightDescription.Modifiers),
            (VariableTerm leftVariable, VariableTerm rightVariable) =>
                leftVariable.Identifier == rightVariable.Identifier,
            (UnresolvedReferenceTerm leftReference, UnresolvedReferenceTerm rightReference) =>
                leftReference.Reference == rightReference.Reference,
            _ => false
        };
    }
}
