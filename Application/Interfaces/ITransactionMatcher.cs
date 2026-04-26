using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface ITransactionMatcher
{
    IReadOnlyList<UdsTransaction> Match(IReadOnlyList<IsoTpMessage> messages);
}
