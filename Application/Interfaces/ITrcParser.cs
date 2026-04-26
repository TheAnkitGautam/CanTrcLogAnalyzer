using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface ITrcParser
{
    Task<IReadOnlyList<CanFrame>> ParseAsync(string filePath, CancellationToken cancellationToken);
}
