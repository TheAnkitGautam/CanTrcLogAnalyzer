using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

internal static class ModelExtensions
{
    public static int LineNumberOrStart(this IsoTpMessage message) => message.StartLineNumber;
}
