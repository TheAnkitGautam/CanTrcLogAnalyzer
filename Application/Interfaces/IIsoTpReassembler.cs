using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface IIsoTpReassembler
{
    IsoTpReassemblyResult Reassemble(IEnumerable<CanFrame> frames);
}

public sealed class IsoTpReassemblyResult
{
    public IReadOnlyList<IsoTpMessage> Messages { get; init; } = [];
    public IReadOnlyList<DiagnosticIssue> Issues { get; init; } = [];
}
