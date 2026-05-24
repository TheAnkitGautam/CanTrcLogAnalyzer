# ISO 14229 TRC Coverage Samples

Generated test logs:

- `iso14229_11bit_full_coverage.trc`
- `iso14229_29bit_full_coverage.trc`

Each file contains:

- All ISO 14229 services currently cataloged by the analyzer:
  `0x10`, `0x11`, `0x14`, `0x19`, `0x22`, `0x23`, `0x24`, `0x27`, `0x28`, `0x29`, `0x2A`, `0x2C`, `0x2E`, `0x2F`, `0x31`, `0x34`, `0x35`, `0x36`, `0x37`, `0x38`, `0x3D`, `0x3E`, `0x83`, `0x84`, `0x85`, `0x86`, `0x87`.
- Positive request/response examples for each service.
- Every possible NRC byte from `0x00` through `0xFF` as a negative response to `ReadDataByIdentifier`.
- Multi-frame requests and responses.
- Flow Control frames.
- Functional addressing with multiple ECU responses.
- Interleaved multi-ECU traffic.
- Timeout scenario.
- Retry pattern.
- Response pending followed by a final positive response.
- Malformed single frame.
- Unexpected consecutive frame.
- Wrong consecutive-frame sequence number.
- Incomplete first frame at end of trace.

Expected analyzer behavior:

- Both files should load without parser errors.
- Both should show 27 unique cataloged services.
- Both should show 258 negative responses: 256 NRC sweep responses plus two edge-case negative responses.
- Both intentionally produce diagnostic issues because the files include negative responses and ISO-TP edge cases.
