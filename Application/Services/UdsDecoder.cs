using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class UdsDecoder(INrcInterpreter nrcInterpreter) : IUdsDecoder
{
    private static readonly IReadOnlyDictionary<byte, string> Services = new Dictionary<byte, string>
    {
        [0x10] = "DiagnosticSessionControl",
        [0x11] = "ECUReset",
        [0x14] = "ClearDiagnosticInformation",
        [0x19] = "ReadDTCInformation",
        [0x22] = "ReadDataByIdentifier",
        [0x27] = "SecurityAccess",
        [0x28] = "CommunicationControl",
        [0x2E] = "WriteDataByIdentifier",
        [0x2F] = "InputOutputControlByIdentifier",
        [0x31] = "RoutineControl",
        [0x34] = "RequestDownload",
        [0x35] = "RequestUpload",
        [0x36] = "TransferData",
        [0x37] = "RequestTransferExit",
        [0x3E] = "TesterPresent",
        [0x85] = "ControlDTCSetting"
    };

    public UdsDecodedMessage? Decode(IsoTpMessage message)
    {
        if (message.Status is IsoTpMessageStatus.FlowControl || message.Payload.Count == 0)
        {
            return null;
        }

        var sid = message.Payload[0];
        if (sid == 0x7F && message.Payload.Count >= 3)
        {
            var originalSid = message.Payload[1];
            var nrc = message.Payload[2];
            var info = nrcInterpreter.Interpret(nrc);
            return new UdsDecodedMessage
            {
                ServiceId = sid,
                OriginalServiceId = originalSid,
                IsNegativeResponse = true,
                NegativeResponseCode = nrc,
                ServiceName = "NegativeResponse",
                Description = $"Negative response to 0x{originalSid:X2} {GetServiceName(originalSid)}",
                NrcMeaning = info.Meaning,
                SuggestedAction = info.SuggestedAction
            };
        }

        if (sid >= 0x40)
        {
            var originalSid = (byte)(sid - 0x40);
            return new UdsDecodedMessage
            {
                ServiceId = sid,
                OriginalServiceId = originalSid,
                IsPositiveResponse = true,
                ServiceName = $"{GetServiceName(originalSid)} positive response",
                Description = $"Positive response to 0x{originalSid:X2}"
            };
        }

        return new UdsDecodedMessage
        {
            ServiceId = sid,
            ServiceName = GetServiceName(sid),
            Description = "UDS request"
        };
    }

    private static string GetServiceName(byte sid) => Services.TryGetValue(sid, out var name) ? name : "UnknownService";
}
