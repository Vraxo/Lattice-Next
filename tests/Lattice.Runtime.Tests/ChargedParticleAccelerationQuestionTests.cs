using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ChargedParticleAccelerationQuestionTests
{
    [Fact]
    public async Task Calculates_proton_final_speed_from_the_potential_difference_sign()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "A proton moving at a speed of 2.5 × 10^4 m/s is accelerated by a potential difference of −250 V. Calculate its final speed.",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(charged-particle-final-speed(proton, initial-speed=25000 m/s, potential-difference=-250 V))",
            answer.Meaning);
        Assert.Contains("2.20 × 10^5 m/s", answer.Message);
        Assert.Contains("3.26 eV", answer.Message);
        Assert.Contains("increases its kinetic energy by 250.00 eV", answer.Message);
        Assert.Contains("253.26 eV", answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }

    [Fact]
    public async Task Abstains_when_the_particle_has_no_physical_data()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "A neutron moving at a speed of 2.5 × 10^4 m/s is accelerated by a potential difference of −250 V. Calculate its final speed.",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
