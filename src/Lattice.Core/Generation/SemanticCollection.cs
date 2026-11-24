using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SemanticCollection(ImmutableArray<SemanticCollectionItem> Items);
