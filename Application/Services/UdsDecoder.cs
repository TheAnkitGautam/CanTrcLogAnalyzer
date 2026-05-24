using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class UdsDecoder(INrcInterpreter nrcInterpreter) : IUdsDecoder
{
    public UdsDecodedMessage? Decode(IsoTpMessage message)
    {
        if (message.Status is IsoTpMessageStatus.FlowControl || message.Payload.Count == 0)
        {
            return null;
        }

        var sid = message.Payload[0];
        if (sid == 0x7F)
        {
            return DecodeNegativeResponse(message);
        }

        if (IsPositiveResponse(message, sid))
        {
            return DecodePositiveResponse(message, sid);
        }

        return DecodeRequest(message, sid);
    }

    private UdsDecodedMessage DecodeNegativeResponse(IsoTpMessage message)
    {
        var originalSid = message.Payload.Count >= 2 ? message.Payload[1] : (byte?)null;
        var nrc = message.Payload.Count >= 3 ? message.Payload[2] : (byte?)null;
        var originalService = originalSid.HasValue ? UdsServiceCatalog.GetService(originalSid.Value) : null;
        var nrcInfo = nrc.HasValue
            ? nrcInterpreter.Interpret(nrc.Value)
            : new NrcInfo("Malformed negative response", "A negative response must contain original SID and NRC.", "Format", false);

        return new UdsDecodedMessage
        {
            ServiceId = 0x7F,
            OriginalServiceId = originalSid,
            IsNegativeResponse = true,
            NegativeResponseCode = nrc,
            ServiceName = "NegativeResponse",
            ServiceLongName = "Negative Response",
            ServiceCategory = "Response",
            ServicePurpose = "The ECU rejected a diagnostic request and returned a reason code.",
            MessageKind = "Negative response",
            Description = originalSid.HasValue
                ? $"Negative response to 0x{originalSid.Value:X2} {originalService?.Name ?? "UnknownService"}: {nrcInfo.Meaning}"
                : $"Malformed negative response: {nrcInfo.Meaning}",
            NrcMeaning = nrcInfo.Meaning,
            NrcCategory = nrcInfo.Category,
            SuggestedAction = nrcInfo.SuggestedAction,
            ParameterSummary = message.Payload.Count >= 3
                ? $"Original SID: 0x{originalSid!.Value:X2}; NRC: 0x{nrc!.Value:X2} ({nrcInfo.Meaning})"
                : "Malformed negative response payload"
        };
    }

    private static UdsDecodedMessage DecodePositiveResponse(IsoTpMessage message, byte sid)
    {
        var originalSid = (byte)(sid - 0x40);
        var service = UdsServiceCatalog.GetService(originalSid);
        var subFunction = TryGetPositiveResponseSubFunction(originalSid, message.Payload);
        var subFunctionInfo = subFunction.HasValue
            ? UdsServiceCatalog.GetSubFunction(originalSid, subFunction.Value)
            : null;

        return new UdsDecodedMessage
        {
            ServiceId = sid,
            OriginalServiceId = originalSid,
            SubFunction = subFunction,
            IsPositiveResponse = true,
            ServiceName = $"{service.Name}PositiveResponse",
            ServiceLongName = $"{service.LongName} positive response",
            ServiceCategory = service.Category,
            ServicePurpose = service.Purpose,
            MessageKind = "Positive response",
            SubFunctionName = subFunctionInfo?.Name ?? "",
            SubFunctionMeaning = subFunctionInfo?.Meaning ?? "",
            Description = $"Positive response to 0x{originalSid:X2} {service.Name}",
            ParameterSummary = UdsServiceCatalog.BuildParameterSummary(originalSid, message.Payload, isResponse: true)
        };
    }

    private static UdsDecodedMessage DecodeRequest(IsoTpMessage message, byte sid)
    {
        var service = UdsServiceCatalog.GetService(sid);
        var subFunction = TryGetRequestSubFunction(sid, message.Payload);
        var subFunctionInfo = subFunction.HasValue
            ? UdsServiceCatalog.GetSubFunction(sid, subFunction.Value)
            : null;
        var suppressPositiveResponse = message.Payload.Count >= 2 && (message.Payload[1] & 0x80) != 0;

        return new UdsDecodedMessage
        {
            ServiceId = sid,
            SubFunction = subFunction,
            ServiceName = service.Name,
            ServiceLongName = service.LongName,
            ServiceCategory = service.Category,
            ServicePurpose = service.Purpose,
            MessageKind = service.IsStandardized ? "Request" : service.Category,
            SubFunctionName = subFunctionInfo?.Name ?? BuildGenericSubFunctionName(subFunction),
            SubFunctionMeaning = subFunctionInfo?.Meaning ?? "",
            Description = BuildRequestDescription(service, subFunctionInfo, suppressPositiveResponse),
            ParameterSummary = UdsServiceCatalog.BuildParameterSummary(sid, message.Payload, isResponse: false)
        };
    }

    private static bool IsPositiveResponse(IsoTpMessage message, byte sid)
    {
        if (sid < 0x40)
        {
            return false;
        }

        var originalSid = (byte)(sid - 0x40);
        return message.IsRx && UdsServiceCatalog.IsKnownRequestService(originalSid);
    }

    private static byte? TryGetRequestSubFunction(byte sid, IReadOnlyList<byte> payload)
    {
        if (payload.Count < 2 || !ServiceUsuallyHasSubFunction(sid))
        {
            return null;
        }

        return (byte)(payload[1] & 0x7F);
    }

    private static byte? TryGetPositiveResponseSubFunction(byte originalSid, IReadOnlyList<byte> payload)
    {
        if (payload.Count < 2 || !ServiceUsuallyHasSubFunction(originalSid))
        {
            return null;
        }

        return (byte)(payload[1] & 0x7F);
    }

    private static bool ServiceUsuallyHasSubFunction(byte sid) =>
        sid is 0x10 or 0x11 or 0x19 or 0x27 or 0x28 or 0x29 or 0x2A or 0x2C or 0x31 or 0x38 or 0x3E or 0x83 or 0x85 or 0x86 or 0x87;

    private static string BuildRequestDescription(UdsServiceInfo service, UdsSubFunctionInfo? subFunction, bool suppressPositiveResponse)
    {
        var description = $"0x{service.ServiceId:X2} {service.LongName}: {service.Purpose}";
        if (subFunction is not null)
        {
            description += $" Sub-function 0x{subFunction.SubFunction:X2} {subFunction.Name}: {subFunction.Meaning}";
        }

        if (suppressPositiveResponse)
        {
            description += " Suppress positive response bit is set.";
        }

        return description;
    }

    private static string BuildGenericSubFunctionName(byte? subFunction) =>
        subFunction.HasValue ? $"SubFunction0x{subFunction.Value:X2}" : "";
}
