using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticWorldModelResponseFormatting;
namespace Lattice.Runtime;

internal static class ChargedParticleAccelerationResponder
{

    internal static bool TryRespond(
        ChargedParticleAccelerationFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var particle = model.FindConcept(request.Particle, SemanticConceptKind.Individual);
        if (particle is null)
        {
            return false;
        }

        var massAssertion = model.GetDataAssertions(particle.Id, "mass-atomic-mass-units").FirstOrDefault();
        var chargeAssertion = model.GetDataAssertions(particle.Id, "charge-elementary-charge-units").FirstOrDefault();
        if (massAssertion is null ||
            chargeAssertion is null ||
            !double.TryParse(massAssertion.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mass) ||
            !double.TryParse(chargeAssertion.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var charge) ||
            !ChargedParticleAccelerationCalculator.TryCalculateFinalSpeed(
                (double)request.InitialSpeedMetersPerSecond,
                (double)request.PotentialDifferenceVolts,
                charge,
                mass,
                out var result,
                out _))
        {
            return false;
        }

        var energyDirection = result!.KineticEnergyChangeElectronVolts >= 0
            ? "increases"
            : "decreases";
        message =
            $"The {particle.Label}'s final speed is approximately {FormatScientific(result.FinalSpeedMetersPerSecond)} m/s. " +
            $"Its initial kinetic energy is about {result.InitialKineticEnergyElectronVolts.ToString("0.00", CultureInfo.InvariantCulture)} eV. " +
            $"Because ΔK = −qΔV, this potential difference {energyDirection} its kinetic energy by " +
            $"{Math.Abs(result.KineticEnergyChangeElectronVolts).ToString("0.00", CultureInfo.InvariantCulture)} eV, " +
            $"giving a final kinetic energy of about {result.FinalKineticEnergyElectronVolts.ToString("0.00", CultureInfo.InvariantCulture)} eV.";

        var sources = reasoner.GetMembershipEvidence(particle.Id, "charged-particle")
            .Add(massAssertion.Source)
            .Add(chargeAssertion.Source)
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }
}
