using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Models;
using EvUdsAnalyzer.Infrastructure.Parsers;

namespace EvUdsAnalyzer.Infrastructure.Channel;

public sealed class EcuChannelResolver : IEcuChannelResolver
{
    public EcuChannel ResolveForFrame(CanFrame frame) =>
        Resolve(frame.CanIdValue, frame.Bus, null);

    public EcuChannel ResolveForMessage(IsoTpMessage message, IsoTpMessage? matchedRequest = null)
    {
        var id = message.CanIdValue;

        if (matchedRequest?.CanIdValue == 0x7DF)
        {
            return new EcuChannel("7DF", FormatCanId(id), message.Bus);
        }

        if (matchedRequest is not null && IsFunctional29Bit(matchedRequest.CanIdValue))
        {
            return IsFunctional29Bit(id)
                ? new EcuChannel(FormatCanId(id), "*", message.Bus)
                : new EcuChannel(FormatCanId(matchedRequest.CanIdValue), FormatCanId(id), message.Bus);
        }

        return Resolve(id, message.Bus, matchedRequest?.CanIdValue);
    }

    private static EcuChannel Resolve(uint canId, int? bus, uint? matchedRequestId)
    {
        if (canId == 0x7DF)
        {
            return new EcuChannel("7DF", "*", bus);
        }

        if (canId <= 0x7FF)
        {
            return Resolve11Bit(canId, bus, matchedRequestId);
        }

        return Resolve29Bit(canId, bus, matchedRequestId);
    }

    private static EcuChannel Resolve11Bit(uint canId, int? bus, uint? matchedRequestId)
    {
        if (canId is >= 0x7E0 and <= 0x7E7)
        {
            return new EcuChannel(FormatCanId(canId), FormatCanId(canId + 0x8), bus);
        }

        if (canId is >= 0x7E8 and <= 0x7EF)
        {
            var requestId = matchedRequestId is not null and not 0x7DF
                ? matchedRequestId.Value
                : canId - 0x8;
            return new EcuChannel(FormatCanId(requestId), FormatCanId(canId), bus);
        }

        if (matchedRequestId is not null)
        {
            return new EcuChannel(FormatCanId(matchedRequestId.Value), FormatCanId(canId), bus);
        }

        var isResponse = TrcParser.InferIsRxFromUdsStyle(canId);
        return isResponse && canId >= 0x8
            ? new EcuChannel(FormatCanId(canId - 0x8), FormatCanId(canId), bus)
            : new EcuChannel(FormatCanId(canId), FormatCanId(canId + 0x8), bus);
    }

    private static EcuChannel Resolve29Bit(uint canId, int? bus, uint? matchedRequestId)
    {
        if (IsFunctional29Bit(canId))
        {
            return new EcuChannel(FormatCanId(canId), "*", bus);
        }

        if (matchedRequestId is not null)
        {
            return new EcuChannel(FormatCanId(matchedRequestId.Value), FormatCanId(canId), bus);
        }

        var pairedId = SwapSourceTarget(canId);
        var isResponse = TrcParser.InferIsRxFromUdsStyle(canId);
        return isResponse
            ? new EcuChannel(FormatCanId(pairedId), FormatCanId(canId), bus)
            : new EcuChannel(FormatCanId(canId), FormatCanId(pairedId), bus);
    }

    private static uint SwapSourceTarget(uint id)
    {
        var baseId = id & 0xFFFF0000;
        var target = (byte)((id >> 8) & 0xFF);
        var source = (byte)(id & 0xFF);
        return baseId | ((uint)source << 8) | target;
    }

    private static bool IsFunctional29Bit(uint id) =>
        (id & 0x1FFF0000) == 0x18DB0000;

    public static byte GetSource(uint id) => (byte)((id >> 8) & 0xFF);

    public static byte GetTarget(uint id) => (byte)(id & 0xFF);

    public static string FormatCanId(uint canId) =>
        canId <= 0x7FF ? canId.ToString("X3") : canId.ToString("X8");
}
