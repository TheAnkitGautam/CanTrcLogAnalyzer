using EvUdsAnalyzer.Domain.Enums;

namespace EvUdsAnalyzer.Infrastructure.IsoTp;

public static class IsoTpUtilities
{
    public static IsoTpFrameType GetFrameType(IReadOnlyList<byte> data)
    {
        if (data.Count == 0)
        {
            return IsoTpFrameType.Unknown;
        }

        return (data[0] >> 4) switch
        {
            0x0 => IsoTpFrameType.SingleFrame,
            0x1 => IsoTpFrameType.FirstFrame,
            0x2 => IsoTpFrameType.ConsecutiveFrame,
            0x3 => IsoTpFrameType.FlowControl,
            _ => IsoTpFrameType.Unknown
        };
    }
}
