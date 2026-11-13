using System.Collections.Immutable;
using Lattice.Client;

namespace Lattice.Runtime;

internal sealed record SoftwareProfile(
    string Name,
    string Role,
    ImmutableArray<string> Capabilities,
    ImmutableArray<string> ApplicationDomains,
    string DocumentationUrl,
    bool IsOptimizationFramework,
    bool HasProcessModelingCapability,
    bool HasOptimizationCapability,
    ImmutableArray<LatticeEvidenceSource> Evidence);
