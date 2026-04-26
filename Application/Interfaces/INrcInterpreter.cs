namespace EvUdsAnalyzer.Application.Interfaces;

public interface INrcInterpreter
{
    NrcInfo Interpret(byte code);
}

public sealed record NrcInfo(string Meaning, string SuggestedAction);
