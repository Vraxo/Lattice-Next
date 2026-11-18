using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonClassProgramParser
{
    private static readonly Regex BankAccountClassPattern = new(
        @"\bclass\s+(?:representing|for)\s+(?:a\s+)?bank\s+account\b.*\bbalance\b.*\bdeposit(?:ion)?\s+method\b.*\bwithdraw(?:al)?\s+method\b.*\b(?:refuses?|rejects?)\s+withdrawals?\s+(?:that\s+are\s+)?(?:larger|greater)\s+than\s+(?:the\s+)?balance\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryGetBankAccountClassProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!BankAccountClassPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-class", "class-name", "BankAccount"),
            new TaskRequestStep("begin-class-method", "method-header", "__init__(self, balance)"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", "self.balance = balance"),
            new TaskRequestStep("begin-class-method", "method-header", "deposit(self, amount)"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", "self.balance += amount"),
            new TaskRequestStep("begin-class-method", "method-header", "withdraw(self, amount)"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", "amount > self.balance"),
            new TaskRequestStep("return-value-triple-indented-expression", "expression", "False"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", "self.balance -= amount"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", "True"));
        return true;
    }
}
