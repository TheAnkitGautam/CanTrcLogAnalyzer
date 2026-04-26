using EvUdsAnalyzer.Application.Interfaces;

namespace EvUdsAnalyzer.Application.Services;

public sealed class NrcInterpreter : INrcInterpreter
{
    private static readonly IReadOnlyDictionary<byte, NrcInfo> Codes = new Dictionary<byte, NrcInfo>
    {
        [0x10] = new("General reject", "Inspect ECU preconditions and request format."),
        [0x11] = new("Service not supported", "Verify the service is supported by the target ECU."),
        [0x12] = new("Sub-function not supported", "Verify the sub-function and active diagnostic session."),
        [0x13] = new("Incorrect message length or invalid format", "Check UDS payload length and parameter encoding."),
        [0x21] = new("Busy repeat request", "Retry after ECU processing time or check bus load."),
        [0x22] = new("Conditions not correct", "Enter the required session or satisfy ECU state preconditions."),
        [0x24] = new("Request sequence error", "Check diagnostic workflow order, especially security/session steps."),
        [0x31] = new("Request out of range", "Validate DID/RID/address values and session permissions."),
        [0x33] = new("Security access denied", "Unlock security access or verify seed/key calculation."),
        [0x35] = new("Invalid key", "Check security algorithm and key freshness."),
        [0x36] = new("Exceeded number of attempts", "Wait for ECU delay timer or power-cycle according to OEM policy."),
        [0x37] = new("Required time delay not expired", "Wait before retrying security access."),
        [0x78] = new("Response pending", "Allow P2* time before declaring a timeout."),
        [0x7E] = new("Sub-function not supported in active session", "Switch to the required diagnostic session."),
        [0x7F] = new("Service not supported in active session", "Switch session or confirm ECU capability.")
    };

    public NrcInfo Interpret(byte code) =>
        Codes.TryGetValue(code, out var info)
            ? info
            : new NrcInfo("Unknown negative response code", "Consult ECU/OEM diagnostic specification.");
}
