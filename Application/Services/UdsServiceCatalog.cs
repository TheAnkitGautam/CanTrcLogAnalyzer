using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public static class UdsServiceCatalog
{
    private static readonly IReadOnlyDictionary<byte, UdsServiceInfo> Services = new Dictionary<byte, UdsServiceInfo>
    {
        [0x10] = Service(0x10, "DiagnosticSessionControl", "Diagnostic Session Control", "Session", "Switches the ECU into a diagnostic session such as default, programming, or extended session.", true),
        [0x11] = Service(0x11, "ECUReset", "ECU Reset", "ECU control", "Requests an ECU reset such as hard reset, key off/on reset, or soft reset.", true),
        [0x14] = Service(0x14, "ClearDiagnosticInformation", "Clear Diagnostic Information", "DTC", "Clears diagnostic trouble code information from ECU memory."),
        [0x19] = Service(0x19, "ReadDTCInformation", "Read DTC Information", "DTC", "Reads diagnostic trouble code status, snapshots, extended data, severity, and related information.", true),
        [0x22] = Service(0x22, "ReadDataByIdentifier", "Read Data By Identifier", "Data", "Reads one or more ECU data identifiers, commonly called DIDs."),
        [0x23] = Service(0x23, "ReadMemoryByAddress", "Read Memory By Address", "Memory", "Reads ECU memory from an address and size encoded in the request."),
        [0x24] = Service(0x24, "ReadScalingDataByIdentifier", "Read Scaling Data By Identifier", "Data", "Reads scaling/format information for a data identifier."),
        [0x27] = Service(0x27, "SecurityAccess", "Security Access", "Security", "Performs seed/key challenge-response unlocking for protected diagnostic functions.", true),
        [0x28] = Service(0x28, "CommunicationControl", "Communication Control", "Communication", "Enables or disables ECU transmit/receive communication behavior.", true),
        [0x29] = Service(0x29, "Authentication", "Authentication", "Security", "Performs certificate-based authentication and de-authentication procedures.", true),
        [0x2A] = Service(0x2A, "ReadDataByPeriodicIdentifier", "Read Data By Periodic Identifier", "Data", "Configures or reads periodic data identifier transmission."),
        [0x2C] = Service(0x2C, "DynamicallyDefineDataIdentifier", "Dynamically Define Data Identifier", "Data", "Creates or clears dynamic DIDs from source DIDs or memory addresses.", true),
        [0x2E] = Service(0x2E, "WriteDataByIdentifier", "Write Data By Identifier", "Data", "Writes a value to a data identifier."),
        [0x2F] = Service(0x2F, "InputOutputControlByIdentifier", "Input Output Control By Identifier", "Control", "Temporarily controls ECU input/output signals by identifier.", true),
        [0x31] = Service(0x31, "RoutineControl", "Routine Control", "Routine", "Starts, stops, or requests results from ECU routines.", true),
        [0x34] = Service(0x34, "RequestDownload", "Request Download", "Upload/download", "Requests permission to download data to the ECU."),
        [0x35] = Service(0x35, "RequestUpload", "Request Upload", "Upload/download", "Requests permission to upload data from the ECU."),
        [0x36] = Service(0x36, "TransferData", "Transfer Data", "Upload/download", "Transfers data blocks during upload or download."),
        [0x37] = Service(0x37, "RequestTransferExit", "Request Transfer Exit", "Upload/download", "Terminates a data transfer and optionally validates transfer parameters."),
        [0x38] = Service(0x38, "RequestFileTransfer", "Request File Transfer", "Upload/download", "Requests file add, delete, replace, read, or directory operations.", true),
        [0x3D] = Service(0x3D, "WriteMemoryByAddress", "Write Memory By Address", "Memory", "Writes ECU memory at an address and size encoded in the request."),
        [0x3E] = Service(0x3E, "TesterPresent", "Tester Present", "Session", "Keeps the diagnostic session active or confirms tester presence.", true),
        [0x7F] = Service(0x7F, "NegativeResponse", "Negative Response", "Response", "Indicates the ECU rejected a request and provides a negative response code."),
        [0x83] = Service(0x83, "AccessTimingParameter", "Access Timing Parameter", "Timing", "Reads or changes diagnostic timing parameters such as P2/P2*."),
        [0x84] = Service(0x84, "SecuredDataTransmission", "Secured Data Transmission", "Security", "Transmits diagnostic data through secured/encrypted wrapping."),
        [0x85] = Service(0x85, "ControlDTCSetting", "Control DTC Setting", "DTC", "Enables or disables DTC setting in the ECU.", true),
        [0x86] = Service(0x86, "ResponseOnEvent", "Response On Event", "Event", "Configures ECU responses triggered by diagnostic events.", true),
        [0x87] = Service(0x87, "LinkControl", "Link Control", "Communication", "Controls diagnostic communication link baudrate or transition behavior.", true)
    };

    private static readonly IReadOnlyDictionary<(byte ServiceId, byte SubFunction), UdsSubFunctionInfo> SubFunctions =
        BuildSubFunctions();

    public static UdsServiceInfo GetService(byte serviceId)
    {
        if (Services.TryGetValue(serviceId, out var service))
        {
            return service;
        }

        return serviceId switch
        {
            <= 0x0F => Service(serviceId, "ReservedOrOBDService", "Reserved / legislated OBD service range", "Reserved", "This service identifier is not a UDS application service in ISO 14229-1.", isStandardized: false),
            0x3F => Service(serviceId, "ReservedService", "Reserved service identifier", "Reserved", "Reserved by ISO/SAE."),
            >= 0x40 and <= 0x7E => Service(serviceId, "PositiveResponseId", "Positive response service identifier", "Response", $"Positive response SID for request 0x{serviceId - 0x40:X2}.", isStandardized: false),
            >= 0x80 and <= 0x82 => Service(serviceId, "ReservedService", "Reserved service identifier", "Reserved", "Reserved by ISO/SAE."),
            >= 0x88 and <= 0xAF => Service(serviceId, "ReservedService", "Reserved service identifier", "Reserved", "Reserved by ISO/SAE for future diagnostic services.", isStandardized: false),
            >= 0xB0 and <= 0xBF => Service(serviceId, "VehicleManufacturerSpecificService", "Vehicle manufacturer specific service", "OEM-specific", "OEM-specific diagnostic service. Decode requires the ECU or OEM diagnostic specification.", isStandardized: false),
            >= 0xC0 and <= 0xFE => Service(serviceId, "PositiveResponseId", "Positive response service identifier", "Response", $"Positive response SID for request 0x{serviceId - 0x40:X2}.", isStandardized: false),
            0xFF => Service(serviceId, "ReservedService", "Reserved service identifier", "Reserved", "Reserved by ISO/SAE."),
            _ => Service(serviceId, "UnknownService", "Unknown service", "Unknown", "No catalog entry is available for this SID.", isStandardized: false)
        };
    }

    public static bool IsKnownRequestService(byte serviceId) =>
        Services.ContainsKey(serviceId) && serviceId != 0x7F;

    public static UdsSubFunctionInfo? GetSubFunction(byte serviceId, byte subFunction)
    {
        var normalizedSubFunction = (byte)(subFunction & 0x7F);
        return SubFunctions.TryGetValue((serviceId, normalizedSubFunction), out var info)
            ? info
            : null;
    }

    public static string BuildParameterSummary(byte serviceId, IReadOnlyList<byte> payload, bool isResponse)
    {
        if (payload.Count == 0)
        {
            return "";
        }

        return serviceId switch
        {
            0x10 when payload.Count >= 2 => $"Session type: 0x{payload[1] & 0x7F:X2}",
            0x11 when payload.Count >= 2 => $"Reset type: 0x{payload[1] & 0x7F:X2}",
            0x14 when payload.Count >= 4 => $"DTC group: {Hex(payload.Skip(1).Take(3))}",
            0x19 when payload.Count >= 2 => $"DTC sub-function 0x{payload[1] & 0x7F:X2}",
            0x22 when isResponse && payload.Count >= 3 => $"DID: 0x{payload[1]:X2}{payload[2]:X2}; data bytes: {Math.Max(0, payload.Count - 3)}",
            0x22 when payload.Count > 1 => $"DID(s): {FormatWords(payload.Skip(1))}",
            0x23 when payload.Count >= 2 => $"Address/length format: 0x{payload[1]:X2}",
            0x24 when payload.Count >= 3 => $"DID: {Hex(payload.Skip(1).Take(2))}",
            0x27 when isResponse && payload.Count >= 2 => $"Security level/sub-function: 0x{payload[1] & 0x7F:X2}; seed/key bytes: {Math.Max(0, payload.Count - 2)}",
            0x27 when payload.Count >= 2 => $"Security level/sub-function: 0x{payload[1] & 0x7F:X2}; key bytes: {Math.Max(0, payload.Count - 2)}",
            0x28 when payload.Count >= 3 => $"Control type: 0x{payload[1] & 0x7F:X2}; communication type: 0x{payload[2]:X2}",
            0x2A when payload.Count >= 2 => $"Transmission mode: 0x{payload[1]:X2}",
            0x2C when payload.Count >= 2 => $"Definition type: 0x{payload[1] & 0x7F:X2}",
            0x2E when payload.Count >= 3 => $"DID: 0x{payload[1]:X2}{payload[2]:X2}; data bytes: {Math.Max(0, payload.Count - 3)}",
            0x2F when payload.Count >= 4 => $"DID: {Hex(payload.Skip(1).Take(2))}; control option: 0x{payload[3]:X2}",
            0x31 when payload.Count >= 4 => $"Routine control type: 0x{payload[1] & 0x7F:X2}; RID: 0x{payload[2]:X2}{payload[3]:X2}",
            0x34 when payload.Count >= 3 => $"Data format: 0x{payload[1]:X2}; address/length format: 0x{payload[2]:X2}",
            0x35 when payload.Count >= 3 => $"Data format: 0x{payload[1]:X2}; address/length format: 0x{payload[2]:X2}",
            0x36 when payload.Count >= 2 => $"Block sequence counter: 0x{payload[1]:X2}; data bytes: {Math.Max(0, payload.Count - 2)}",
            0x38 when payload.Count >= 2 => $"Mode of operation: 0x{payload[1] & 0x7F:X2}",
            0x3D when payload.Count >= 2 => $"Address/length format: 0x{payload[1]:X2}; data bytes: {Math.Max(0, payload.Count - 2)}",
            0x3E when payload.Count >= 2 => $"TesterPresent sub-function: 0x{payload[1] & 0x7F:X2}",
            0x83 when payload.Count >= 2 => $"Timing parameter access type: 0x{payload[1] & 0x7F:X2}",
            0x85 when payload.Count >= 2 => $"DTC setting type: 0x{payload[1] & 0x7F:X2}",
            0x86 when payload.Count >= 2 => $"ResponseOnEvent type: 0x{payload[1] & 0x7F:X2}",
            0x87 when payload.Count >= 2 => $"Link control type: 0x{payload[1] & 0x7F:X2}",
            _ => isResponse ? $"Response data bytes: {Math.Max(0, payload.Count - 1)}" : $"Parameter bytes: {Math.Max(0, payload.Count - 1)}"
        };
    }

    private static Dictionary<(byte ServiceId, byte SubFunction), UdsSubFunctionInfo> BuildSubFunctions()
    {
        var items = new Dictionary<(byte, byte), UdsSubFunctionInfo>();
        Add(items, 0x10, 0x01, "defaultSession", "Default diagnostic session.");
        Add(items, 0x10, 0x02, "programmingSession", "Session used for ECU programming.");
        Add(items, 0x10, 0x03, "extendedDiagnosticSession", "Session used for extended diagnostics.");
        Add(items, 0x10, 0x04, "safetySystemDiagnosticSession", "Session used for safety-system diagnostics when supported.");
        Add(items, 0x11, 0x01, "hardReset", "Forces ECU hard reset.");
        Add(items, 0x11, 0x02, "keyOffOnReset", "Simulates ignition key off/on reset.");
        Add(items, 0x11, 0x03, "softReset", "Requests software reset.");
        Add(items, 0x19, 0x01, "reportNumberOfDTCByStatusMask", "Reports number of DTCs matching status mask.");
        Add(items, 0x19, 0x02, "reportDTCByStatusMask", "Reports DTCs matching status mask.");
        Add(items, 0x19, 0x03, "reportDTCSnapshotIdentification", "Reports DTC snapshot record identifiers.");
        Add(items, 0x19, 0x04, "reportDTCSnapshotRecordByDTCNumber", "Reports snapshot data for a DTC.");
        Add(items, 0x19, 0x05, "reportDTCStoredDataByRecordNumber", "Reports stored DTC data by record number.");
        Add(items, 0x19, 0x06, "reportDTCExtDataRecordByDTCNumber", "Reports extended data for a DTC.");
        Add(items, 0x19, 0x07, "reportNumberOfDTCBySeverityMaskRecord", "Reports number of DTCs matching severity/status.");
        Add(items, 0x19, 0x08, "reportDTCBySeverityMaskRecord", "Reports DTCs matching severity/status.");
        Add(items, 0x19, 0x09, "reportSeverityInformationOfDTC", "Reports severity information for a DTC.");
        Add(items, 0x19, 0x0A, "reportSupportedDTC", "Reports supported DTCs.");
        Add(items, 0x19, 0x0B, "reportFirstTestFailedDTC", "Reports first test-failed DTC.");
        Add(items, 0x19, 0x0C, "reportFirstConfirmedDTC", "Reports first confirmed DTC.");
        Add(items, 0x19, 0x0D, "reportMostRecentTestFailedDTC", "Reports most recent test-failed DTC.");
        Add(items, 0x19, 0x0E, "reportMostRecentConfirmedDTC", "Reports most recent confirmed DTC.");
        Add(items, 0x19, 0x0F, "reportMirrorMemoryDTCByStatusMask", "Reports mirror memory DTCs by status mask.");
        Add(items, 0x19, 0x10, "reportMirrorMemoryDTCExtDataRecordByDTCNumber", "Reports mirror memory extended data.");
        Add(items, 0x19, 0x11, "reportNumberOfMirrorMemoryDTCByStatusMask", "Reports number of mirror memory DTCs.");
        Add(items, 0x19, 0x12, "reportNumberOfEmissionsRelatedOBDDTCByStatusMask", "Reports number of emissions-related OBD DTCs.");
        Add(items, 0x19, 0x13, "reportEmissionsRelatedOBDDTCByStatusMask", "Reports emissions-related OBD DTCs.");
        Add(items, 0x19, 0x14, "reportDTCFaultDetectionCounter", "Reports DTC fault detection counters.");
        Add(items, 0x19, 0x15, "reportDTCWithPermanentStatus", "Reports DTCs with permanent status.");
        Add(items, 0x19, 0x16, "reportDTCExtDataRecordByRecordNumber", "Reports extended data by record number.");
        Add(items, 0x19, 0x17, "reportUserDefMemoryDTCByStatusMask", "Reports user-defined memory DTCs by status mask.");
        Add(items, 0x19, 0x18, "reportUserDefMemoryDTCSnapshotRecordByDTCNumber", "Reports user-defined memory snapshots.");
        Add(items, 0x19, 0x19, "reportUserDefMemoryDTCExtDataRecordByDTCNumber", "Reports user-defined memory extended data.");
        Add(items, 0x19, 0x42, "reportWWHOBDDTCByMaskRecord", "Reports WWH-OBD DTCs by mask record.");
        Add(items, 0x27, 0x01, "requestSeed", "Requests a security seed for level 1.");
        Add(items, 0x27, 0x02, "sendKey", "Sends a security key for level 1.");
        Add(items, 0x28, 0x00, "enableRxAndTx", "Enables receiving and transmitting.");
        Add(items, 0x28, 0x01, "enableRxAndDisableTx", "Enables receiving and disables transmitting.");
        Add(items, 0x28, 0x02, "disableRxAndEnableTx", "Disables receiving and enables transmitting.");
        Add(items, 0x28, 0x03, "disableRxAndTx", "Disables receiving and transmitting.");
        Add(items, 0x28, 0x04, "enableRxAndDisableTxWithEnhancedAddressInformation", "Enables receiving and disables selected enhanced-address transmissions.");
        Add(items, 0x28, 0x05, "enableRxAndTxWithEnhancedAddressInformation", "Enables selected enhanced-address communication.");
        Add(items, 0x29, 0x00, "deAuthenticate", "Terminates authentication.");
        Add(items, 0x29, 0x01, "verifyCertificateUnidirectional", "Performs unidirectional certificate verification.");
        Add(items, 0x29, 0x02, "verifyCertificateBidirectional", "Performs bidirectional certificate verification.");
        Add(items, 0x29, 0x03, "proofOfOwnership", "Verifies proof of ownership.");
        Add(items, 0x29, 0x04, "transmitCertificate", "Transmits a certificate.");
        Add(items, 0x29, 0x05, "requestChallengeForAuthentication", "Requests authentication challenge data.");
        Add(items, 0x29, 0x06, "verifyProofOfOwnershipUnidirectional", "Verifies unidirectional proof of ownership.");
        Add(items, 0x29, 0x07, "verifyProofOfOwnershipBidirectional", "Verifies bidirectional proof of ownership.");
        Add(items, 0x2C, 0x01, "defineByIdentifier", "Defines a dynamic DID using source DIDs.");
        Add(items, 0x2C, 0x02, "defineByMemoryAddress", "Defines a dynamic DID using memory addresses.");
        Add(items, 0x2C, 0x03, "clearDynamicallyDefinedDataIdentifier", "Clears dynamic DID definitions.");
        Add(items, 0x31, 0x01, "startRoutine", "Starts a routine.");
        Add(items, 0x31, 0x02, "stopRoutine", "Stops a routine.");
        Add(items, 0x31, 0x03, "requestRoutineResults", "Requests routine results.");
        Add(items, 0x38, 0x01, "addFile", "Adds a file.");
        Add(items, 0x38, 0x02, "deleteFile", "Deletes a file.");
        Add(items, 0x38, 0x03, "replaceFile", "Replaces a file.");
        Add(items, 0x38, 0x04, "readFile", "Reads a file.");
        Add(items, 0x38, 0x05, "readDir", "Reads a directory.");
        Add(items, 0x3E, 0x00, "zeroSubFunction", "Tester present response required.");
        Add(items, 0x80, 0x00, "suppressPositiveResponse", "Suppresses positive response when used as bit 7 on applicable services.");
        Add(items, 0x83, 0x01, "readExtendedTimingParameterSet", "Reads extended timing parameters.");
        Add(items, 0x83, 0x02, "setTimingParametersToDefaultValues", "Sets timing parameters to defaults.");
        Add(items, 0x83, 0x03, "readCurrentlyActiveTimingParameters", "Reads currently active timing parameters.");
        Add(items, 0x83, 0x04, "setTimingParametersToGivenValues", "Sets timing parameters to supplied values.");
        Add(items, 0x85, 0x01, "on", "Enables DTC setting.");
        Add(items, 0x85, 0x02, "off", "Disables DTC setting.");
        Add(items, 0x86, 0x00, "stopResponseOnEvent", "Stops response-on-event.");
        Add(items, 0x86, 0x01, "onDTCStatusChange", "Responds when DTC status changes.");
        Add(items, 0x86, 0x02, "onTimerInterrupt", "Responds on timer interrupt.");
        Add(items, 0x86, 0x03, "onChangeOfDataIdentifier", "Responds when DID data changes.");
        Add(items, 0x86, 0x04, "reportActivatedEvents", "Reports activated events.");
        Add(items, 0x86, 0x05, "startResponseOnEvent", "Starts response-on-event.");
        Add(items, 0x86, 0x06, "clearResponseOnEvent", "Clears response-on-event.");
        Add(items, 0x86, 0x07, "onComparisonOfValues", "Responds on comparison of values.");
        Add(items, 0x87, 0x01, "verifyBaudrateTransitionWithFixedBaudrate", "Verifies transition to a fixed baudrate.");
        Add(items, 0x87, 0x02, "verifyBaudrateTransitionWithSpecificBaudrate", "Verifies transition to a specific baudrate.");
        Add(items, 0x87, 0x03, "transitionBaudrate", "Transitions baudrate.");
        return items;
    }

    private static UdsServiceInfo Service(byte sid, string name, string longName, string category, string purpose, bool allowsSuppressPositiveResponse = false, bool isStandardized = true) =>
        new(sid, name, longName, category, purpose, isStandardized, allowsSuppressPositiveResponse);

    private static void Add(IDictionary<(byte, byte), UdsSubFunctionInfo> items, byte serviceId, byte subFunction, string name, string meaning) =>
        items[(serviceId, subFunction)] = new UdsSubFunctionInfo(serviceId, subFunction, name, meaning);

    private static string Hex(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(b => b.ToString("X2")));

    private static string FormatWords(IEnumerable<byte> bytes)
    {
        var data = bytes.ToArray();
        if (data.Length == 0)
        {
            return "-";
        }

        if (data.Length % 2 != 0)
        {
            return Hex(data);
        }

        return string.Join(", ", data.Chunk(2).Select(pair => $"0x{pair[0]:X2}{pair[1]:X2}"));
    }
}
