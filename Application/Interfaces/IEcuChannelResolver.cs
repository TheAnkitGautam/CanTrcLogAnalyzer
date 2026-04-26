using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface IEcuChannelResolver
{
    EcuChannel ResolveForFrame(CanFrame frame);
    EcuChannel ResolveForMessage(IsoTpMessage message, IsoTpMessage? matchedRequest = null);
}
