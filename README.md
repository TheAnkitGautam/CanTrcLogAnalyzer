# EV UDS TRC Log Analyzer

Production-oriented WPF desktop analyzer for PCAN `.trc` logs containing UDS over CAN.

## Capabilities

- Parses PCAN trace formats with legacy fixed columns and newer `$COLUMNS` headers.
- Handles ISO-TP Single Frame, First Frame, Consecutive Frame, and Flow Control frames.
- Reassembles multi-frame UDS payloads per bus, CAN ID, and direction so interleaved ECUs do not mix.
- Validates ISO-TP sequence numbers and expected multi-frame payload length.
- Supports physical channels such as `7E0 <-> 7E8` and functional requests such as `7DF -> ECU response`.
- Decodes UDS service IDs, positive responses, negative responses, NRC meaning, and suggested action.
- Matches request/response transactions by ECU channel, timing window, and service relationship.
- Detects timeouts, retries, negative responses, ISO-TP errors, and simple session-precondition issues.
- Uses a rule-engine pattern so new diagnostic insights can be added without changing parser or UI code.
- Opens to a guided dashboard with a plain-English health verdict, top findings, ECU health cards, and recommended next actions.
- Provides search and filters across dashboard, issues, transactions, reconstructed messages, and raw frames.
- Includes a built-in glossary for common UDS, ISO-TP, and diagnostic terms.
- Exports filtered diagnostic results to HTML and CSV.

## Architecture

```text
EvUdsAnalyzer
├── UI
│   ├── Commands
│   └── ViewModels
├── Application
│   ├── Interfaces
│   └── Services
│       └── Rules
├── Domain
│   ├── Enums
│   └── Models
└── Infrastructure
    ├── Channel
    ├── IsoTp
    └── Parsers
```

## Important Classes

- `TrcParser`: async PCAN `.trc` parser.
- `IsoTpReassembler`: stream-safe ISO-TP reassembly and validation engine.
- `EcuChannelResolver`: physical and functional ECU channel mapping.
- `UdsDecoder`: UDS SID, positive response, negative response, and NRC decoding.
- `TransactionMatcher`: request/response pairing with service and timing checks.
- `DiagnosticAnalyzer`: runs pluggable `IDiagnosticRule` implementations.
- `ExplanationService`: converts protocol issues and transactions into plain-English summaries, likely causes, evidence, and action checklists.
- `ReportExportService`: writes HTML reports and CSV exports from the currently visible analysis data.
- `MainViewModel`: MVVM bridge between the analyzer and WPF grids.

## Extendability

Add new diagnostic rules by implementing `IDiagnosticRule` and registering the rule in `CompositionRoot`.

Add UDS services by extending the service-name dictionary in `UdsDecoder`.

Add NRC details by extending `NrcInterpreter`.

Future timing work such as P2/P2* can be added in `TransactionMatcher` or as a separate rule that consumes matched transactions.

## Build

```powershell
dotnet build
```

The application targets `.NET 8` WPF on Windows.
