namespace Lattice.Core.Semantics;

public sealed record ObjectiveMoneyValue(decimal Amount, string CurrencySymbol) : ObjectiveConditionValue;
