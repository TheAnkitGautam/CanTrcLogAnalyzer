using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class DiagnosticAnalyzer(IEnumerable<IDiagnosticRule> rules)
{
    public IReadOnlyList<DiagnosticIssue> Analyze(DiagnosticContext context) =>
        context.ExistingIssues
            .Concat(rules.SelectMany(rule => rule.Evaluate(context)))
            .OrderBy(issue => issue.LineNumber)
            .ThenByDescending(issue => issue.Severity)
            .ToArray();
}
