using EvUdsAnalyzer.Application.Interfaces;

namespace EvUdsAnalyzer.Application.Services;

public sealed class NrcInterpreter : INrcInterpreter
{
    private static readonly IReadOnlyDictionary<byte, NrcInfo> Codes = new Dictionary<byte, NrcInfo>
    {
        [0x00] = new("Positive response parameter value", "This value is reserved for positive-response internals and should not appear as a negative response code.", "Reserved"),
        [0x10] = new("General reject", "Inspect ECU preconditions and request format.", "General"),
        [0x11] = new("Service not supported", "Verify the service is supported by the target ECU.", "Service support"),
        [0x12] = new("Sub-function not supported", "Verify the sub-function and active diagnostic session.", "Service support"),
        [0x13] = new("Incorrect message length or invalid format", "Check UDS payload length and parameter encoding.", "Format"),
        [0x14] = new("Response too long", "Reduce requested data size or split the diagnostic request.", "Transport/data size"),
        [0x21] = new("Busy repeat request", "Retry after ECU processing time or check bus load.", "Timing"),
        [0x22] = new("Conditions not correct", "Enter the required session or satisfy ECU state preconditions.", "Precondition"),
        [0x24] = new("Request sequence error", "Check diagnostic workflow order, especially security/session steps.", "Sequence"),
        [0x25] = new("No response from sub-net component", "Check downstream network/component availability behind the responding gateway.", "Gateway/sub-net"),
        [0x26] = new("Failure prevents execution of requested action", "Read DTC/status information and resolve the ECU failure preventing execution.", "Precondition"),
        [0x31] = new("Request out of range", "Validate DID/RID/address values and session permissions.", "Range"),
        [0x33] = new("Security access denied", "Unlock security access or verify seed/key calculation.", "Security"),
        [0x35] = new("Invalid key", "Check security algorithm and key freshness.", "Security"),
        [0x36] = new("Exceeded number of attempts", "Wait for ECU delay timer or power-cycle according to OEM policy.", "Security"),
        [0x37] = new("Required time delay not expired", "Wait before retrying security access.", "Security"),
        [0x38] = new("Secure data transmission required", "Use SecuredDataTransmission or the required secure diagnostic channel.", "Security"),
        [0x39] = new("Secure data transmission not allowed", "Send this request outside secured wrapping or verify ECU security policy.", "Security"),
        [0x3A] = new("Secure data verification failed", "Check secure message freshness, signature/MAC, and encrypted payload integrity.", "Security"),
        [0x50] = new("Certificate verification failed: invalid time period", "Check certificate validity dates and ECU time source.", "Authentication"),
        [0x51] = new("Certificate verification failed: invalid signature", "Verify certificate signature and trust chain.", "Authentication"),
        [0x52] = new("Certificate verification failed: invalid chain of trust", "Install or use a certificate chain trusted by the ECU.", "Authentication"),
        [0x53] = new("Certificate verification failed: invalid type", "Use the certificate type expected by the ECU.", "Authentication"),
        [0x54] = new("Certificate verification failed: invalid format", "Check certificate encoding and transport formatting.", "Authentication"),
        [0x55] = new("Certificate verification failed: invalid content", "Check certificate fields, subject, issuer, and extensions.", "Authentication"),
        [0x56] = new("Certificate verification failed: invalid scope", "Use a certificate authorized for this ECU/function.", "Authentication"),
        [0x57] = new("Certificate verification failed: invalid certificate", "Replace or reissue the certificate.", "Authentication"),
        [0x58] = new("Ownership verification failed", "Verify proof-of-ownership material and challenge response.", "Authentication"),
        [0x59] = new("Challenge calculation failed", "Check authentication challenge inputs and cryptographic configuration.", "Authentication"),
        [0x5A] = new("Setting access rights failed", "Verify authorization profile and ECU access-right storage.", "Authentication"),
        [0x5B] = new("Session key creation or derivation failed", "Check certificate/key material and cryptographic algorithm configuration.", "Authentication"),
        [0x5C] = new("Configuration data usage failed", "Check authentication configuration data and ECU policy.", "Authentication"),
        [0x5D] = new("De-authentication failed", "Retry de-authentication or verify authentication state.", "Authentication"),
        [0x70] = new("Upload/download not accepted", "Verify memory address, size, data format, and programming preconditions.", "Upload/download"),
        [0x71] = new("Transfer data suspended", "Check transfer sequence, ECU state, and network stability.", "Upload/download"),
        [0x72] = new("General programming failure", "Inspect ECU programming conditions, flash status, and voltage stability.", "Programming"),
        [0x73] = new("Wrong block sequence counter", "Check TransferData block counter ordering and retransmission behavior.", "Upload/download"),
        [0x78] = new("Response pending", "Allow P2* time before declaring a timeout.", "Timing"),
        [0x7E] = new("Sub-function not supported in active session", "Switch to the required diagnostic session.", "Session"),
        [0x7F] = new("Service not supported in active session", "Switch session or confirm ECU capability.", "Session"),
        [0x81] = new("RPM too high", "Bring engine/motor speed below the ECU threshold and retry.", "Vehicle condition"),
        [0x82] = new("RPM too low", "Bring engine/motor speed above the ECU threshold and retry.", "Vehicle condition"),
        [0x83] = new("Engine is running", "Stop the engine/motor if the routine requires it.", "Vehicle condition"),
        [0x84] = new("Engine is not running", "Start the engine/motor if the routine requires it.", "Vehicle condition"),
        [0x85] = new("Engine run time too low", "Wait until minimum run time is reached.", "Vehicle condition"),
        [0x86] = new("Temperature too high", "Wait for temperature to drop below the ECU threshold.", "Vehicle condition"),
        [0x87] = new("Temperature too low", "Raise temperature above the ECU threshold if required.", "Vehicle condition"),
        [0x88] = new("Vehicle speed too high", "Reduce vehicle speed and retry.", "Vehicle condition"),
        [0x89] = new("Vehicle speed too low", "Increase vehicle speed if the routine requires it.", "Vehicle condition"),
        [0x8A] = new("Throttle/pedal too high", "Release accelerator/throttle below the ECU threshold.", "Vehicle condition"),
        [0x8B] = new("Throttle/pedal too low", "Apply accelerator/throttle above the ECU threshold if required.", "Vehicle condition"),
        [0x8C] = new("Transmission range not in neutral", "Place transmission in neutral and retry.", "Vehicle condition"),
        [0x8D] = new("Transmission range not in gear", "Place transmission in the required gear and retry.", "Vehicle condition"),
        [0x8F] = new("Brake switch not closed", "Press brake pedal or verify brake switch status.", "Vehicle condition"),
        [0x90] = new("Shifter lever not in park", "Move shifter to park and retry.", "Vehicle condition"),
        [0x91] = new("Torque converter clutch locked", "Meet drivetrain condition for torque converter clutch state.", "Vehicle condition"),
        [0x92] = new("Voltage too high", "Bring supply voltage below ECU threshold.", "Vehicle condition"),
        [0x93] = new("Voltage too low", "Stabilize battery/supply voltage above ECU threshold.", "Vehicle condition"),
        [0x94] = new("Resource temporarily not available", "Retry after the ECU resource becomes available.", "Resource")
    };

    public NrcInfo Interpret(byte code)
    {
        if (Codes.TryGetValue(code, out var info))
        {
            return info;
        }

        if (code is >= 0x01 and <= 0x0F ||
            code is >= 0x15 and <= 0x20 ||
            code is >= 0x27 and <= 0x30 ||
            code is >= 0x3B and <= 0x4F ||
            code is >= 0x5E and <= 0x6F ||
            code is >= 0x74 and <= 0x77 ||
            code is >= 0x79 and <= 0x7D ||
            code == 0x80 ||
            code == 0x8E ||
            code is >= 0x95 and <= 0xEF ||
            code == 0xFF)
        {
            return new NrcInfo("Reserved negative response code", "Consult the ISO 14229 version and ECU/OEM diagnostic specification before interpreting this value.", "Reserved", false);
        }

        if (code is >= 0xF0 and <= 0xFE)
        {
            return new NrcInfo("Vehicle manufacturer specific conditions not correct", "Consult the OEM diagnostic specification for the exact condition represented by this NRC.", "OEM-specific", false);
        }

        return new NrcInfo("Unassigned negative response code", "Consult ECU/OEM diagnostic specification.", "Unknown", false);
    }
}
