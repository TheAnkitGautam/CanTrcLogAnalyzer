using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Infrastructure.Channel;

public sealed class EcuChannelResolver : IEcuChannelResolver
{
    public EcuChannel ResolveForFrame(CanFrame frame) => Resolve(frame.CanIdValue, frame.Bus, null);

    public EcuChannel ResolveForMessage(IsoTpMessage message, IsoTpMessage? matchedRequest = null)
    {
        if (matchedRequest?.CanIdValue == 0x7DF)
        {
            return new EcuChannel("7DF", FormatCanId(message.CanIdValue), message.Bus);
        }

        return Resolve(message.CanIdValue, message.Bus, matchedRequest?.CanIdValue);
    }

    private static EcuChannel Resolve(uint canId, int? bus, uint? matchedRequestId)
    {
        if (canId == 0x7DF)
        {
            return new EcuChannel("7DF", "*", bus);
        }

        if (canId is >= 0x7E0 and <= 0x7E7)
        {
            return new EcuChannel(FormatCanId(canId), FormatCanId(canId + 8), bus);
        }

        if (canId is >= 0x7E8 and <= 0x7EF)
        {
            var requestId = matchedRequestId == 0x7DF ? 0x7DF : canId - 8;
            return new EcuChannel(FormatCanId(requestId), FormatCanId(canId), bus);
        }

        return new EcuChannel(FormatCanId(matchedRequestId ?? canId), FormatCanId(canId), bus);
    }

    private static string FormatCanId(uint canId) => canId <= 0x7FF ? canId.ToString("X3") : canId.ToString("X8");
}
