using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface ILogAnalysisService
{
    Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken);
}
