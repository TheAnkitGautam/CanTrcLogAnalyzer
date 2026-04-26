using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Infrastructure.Channel;

public sealed class EcuChannelResolver : IEcuChannelResolver
{
    public EcuChannel ResolveForFrame(CanFrame frame) =>
        Resolve(frame.CanIdValue, frame.Bus, null);

    public EcuChannel ResolveForMessage(IsoTpMessage message, IsoTpMessage? matchedRequest = null)
    {
        var id = message.CanIdValue;

        // =========================
        // Functional 11-bit
        // =========================
        if (matchedRequest?.CanIdValue == 0x7DF)
        {
            return new EcuChannel("7DF", FormatCanId(id), message.Bus);
        }

        // =========================
        // Functional 29-bit
        // =========================
        if (matchedRequest != null && IsFunctional29Bit(matchedRequest.CanIdValue))
        {
            // Prevent functional ↔ functional
            if (IsFunctional29Bit(id))
            {
                return new EcuChannel(FormatCanId(id), "*", message.Bus);
            }

            return new EcuChannel(
                FormatCanId(matchedRequest.CanIdValue),
                FormatCanId(id),
                message.Bus);
        }

        return Resolve(id, message.Bus, matchedRequest?.CanIdValue);
    }

    private static EcuChannel Resolve(uint canId, int? bus, uint? matchedRequestId)
    {
        // =========================
        // 11-bit UDS
        // =========================
        if (!IsExtended(canId))
        {
            if (canId == 0x7DF)
                return new EcuChannel("7DF", "*", bus);

            // Request
            if (canId is >= 0x7E0 and <= 0x7E7)
                return new EcuChannel(FormatCanId(canId), FormatCanId(canId + 8), bus);

            // Response
            if (canId is >= 0x7E8 and <= 0x7EF)
            {
                if (matchedRequestId is >= 0x7E0 and <= 0x7E7)
                {
                    return new EcuChannel(
                        FormatCanId(matchedRequestId.Value),
                        FormatCanId(canId),
                        bus);
                }

                // fallback (still normalize direction)
                return new EcuChannel(
                    FormatCanId(canId - 8),
                    FormatCanId(canId),
                    bus);
            }
        }

        // =========================
        // 29-bit UDS
        // =========================
        if (IsUds29Bit(canId))
        {
            // If we have a matched request → always trust it
            if (matchedRequestId != null && IsUds29Bit(matchedRequestId.Value))
            {
                return new EcuChannel(
                    FormatCanId(matchedRequestId.Value),
                    FormatCanId(canId),
                    bus);
            }

            // If NO matched request → avoid standalone response channels
            if (!IsLikelyRequest(canId))
            {
                return new EcuChannel(
                    FormatCanId(canId),
                    "*",
                    bus);
            }

            // Request → infer response
            var src = GetSource(canId);
            var tgt = GetTarget(canId);

            uint responseId = Build29BitId(canId, tgt, src);

            return new EcuChannel(
                FormatCanId(canId),
                FormatCanId(responseId),
                bus);
        }

        // =========================
        // Non-UDS (J1939 / unknown)
        // =========================
        return new EcuChannel(
            FormatCanId(canId),
            "*",
            bus);
    }

    // =========================
    // Helpers
    // =========================

    private static bool IsExtended(uint id) => id > 0x7FF;

    private static bool IsUds29Bit(uint id)
    {
        return (id & 0x1FFF0000) == 0x18DA0000 ||
               (id & 0x1FFF0000) == 0x18DB0000;
    }

    private static bool IsFunctional29Bit(uint id)
    {
        return (id & 0x1FFF0000) == 0x18DB0000;
    }

    private static byte GetSource(uint id) => (byte)((id >> 8) & 0xFF);

    private static byte GetTarget(uint id) => (byte)(id & 0xFF);

    private static uint Build29BitId(uint originalId, byte newSource, byte newTarget)
    {
        uint baseId = originalId & 0xFFFF0000;
        return baseId | ((uint)newSource << 8) | newTarget;
    }

    private static bool IsLikelyRequest(uint id)
    {
        var src = GetSource(id);
        var tgt = GetTarget(id);

        // heuristic: tester usually higher address (F1 > ECU IDs)
        return src > tgt;
    }

    private static string FormatCanId(uint canId) =>
        canId <= 0x7FF ? canId.ToString("X3") : canId.ToString("X8");
}