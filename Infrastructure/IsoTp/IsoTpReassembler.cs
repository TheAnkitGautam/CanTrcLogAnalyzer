using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Infrastructure.IsoTp;

public sealed class IsoTpReassembler : IIsoTpReassembler
{
    public IsoTpReassemblyResult Reassemble(IEnumerable<CanFrame> frames)
    {
        var messages = new List<IsoTpMessage>();
        var issues = new List<DiagnosticIssue>();
        var active = new Dictionary<string, ActiveMessage>();

        foreach (var frame in frames.OrderBy(f => f.TimestampMs).ThenBy(f => f.LineNumber))
        {
            if (frame.Data.Length == 0)
            {
                continue;
            }

            var streamKey = $"{frame.Bus?.ToString() ?? "-"}:{frame.Direction}:{frame.CanId}";
            switch (frame.IsoTpFrameType)
            {
                case IsoTpFrameType.SingleFrame:
                    CloseActiveAsIncomplete(active, streamKey, frame, messages, issues, "New single frame arrived before previous multi-frame message completed.");
                    AddSingleFrame(frame, messages, issues);
                    break;
                case IsoTpFrameType.FirstFrame:
                    CloseActiveAsIncomplete(active, streamKey, frame, messages, issues, "New first frame arrived before previous multi-frame message completed.");
                    StartFirstFrame(frame, active, streamKey, messages, issues);
                    break;
                case IsoTpFrameType.ConsecutiveFrame:
                    ConsumeConsecutiveFrame(frame, active, streamKey, messages, issues);
                    break;
                case IsoTpFrameType.FlowControl:
                    messages.Add(new IsoTpMessage
                    {
                        CanId = frame.CanId,
                        CanIdValue = frame.CanIdValue,
                        IsRx = frame.IsRx,
                        Bus = frame.Bus,
                        StartTimeMs = frame.TimestampMs,
                        EndTimeMs = frame.TimestampMs,
                        StartLineNumber = frame.LineNumber,
                        EndLineNumber = frame.LineNumber,
                        FrameType = IsoTpFrameType.FlowControl,
                        Status = IsoTpMessageStatus.FlowControl,
                        Payload = []
                    });
                    break;
                default:
                    issues.Add(Issue(frame, IssueSeverity.Warning, "Unknown ISO-TP PCI", "The frame does not contain a supported ISO-TP PCI nibble."));
                    break;
            }
        }

        foreach (var pending in active.Values)
        {
            var message = pending.ToMessage(IsoTpMessageStatus.Incomplete, "End of file before all consecutive frames were received.");
            messages.Add(message);
            issues.Add(new DiagnosticIssue
            {
                Title = "Incomplete ISO-TP message",
                Description = $"Expected {pending.ExpectedLength} payload bytes but received {pending.Payload.Count}. Possible frame loss or bus congestion.",
                Severity = IssueSeverity.Error,
                LineNumber = pending.StartLineNumber,
                TimestampMs = pending.StartTimeMs,
                Channel = pending.CanId
            });
        }

        return new IsoTpReassemblyResult { Messages = messages, Issues = issues };
    }

    private static void AddSingleFrame(CanFrame frame, List<IsoTpMessage> messages, List<DiagnosticIssue> issues)
    {
        var length = frame.Data[0] & 0x0F;
        var offset = 1;

        if (length == 0 && frame.Data.Length > 1)
        {
            length = frame.Data[1];
            offset = 2;
        }

        if (frame.Data.Length - offset < length)
        {
            issues.Add(Issue(frame, IssueSeverity.Error, "Malformed single frame", $"Single frame announces {length} bytes but only {frame.Data.Length - offset} data bytes are present."));
            length = Math.Max(0, frame.Data.Length - offset);
        }

        messages.Add(new IsoTpMessage
        {
            CanId = frame.CanId,
            CanIdValue = frame.CanIdValue,
            IsRx = frame.IsRx,
            Bus = frame.Bus,
            StartTimeMs = frame.TimestampMs,
            EndTimeMs = frame.TimestampMs,
            StartLineNumber = frame.LineNumber,
            EndLineNumber = frame.LineNumber,
            FrameType = IsoTpFrameType.SingleFrame,
            Status = IsoTpMessageStatus.Complete,
            Payload = frame.Data.Skip(offset).Take(length).ToArray()
        });
    }

    private static void StartFirstFrame(CanFrame frame, Dictionary<string, ActiveMessage> active, string streamKey, List<IsoTpMessage> messages, List<DiagnosticIssue> issues)
    {
        if (frame.Data.Length < 2)
        {
            issues.Add(Issue(frame, IssueSeverity.Error, "Malformed first frame", "First frame must contain a two-byte PCI length field."));
            return;
        }

        var expectedLength = ((frame.Data[0] & 0x0F) << 8) | frame.Data[1];
        if (expectedLength <= 6)
        {
            issues.Add(Issue(frame, IssueSeverity.Warning, "Suspicious first frame length", $"First frame length is {expectedLength}, which normally fits in a single frame."));
        }

        var pending = new ActiveMessage(frame, expectedLength);
        pending.Payload.AddRange(frame.Data.Skip(2).Take(expectedLength));

        if (pending.Payload.Count >= expectedLength)
        {
            messages.Add(pending.ToMessage(IsoTpMessageStatus.Complete));
            return;
        }

        active[streamKey] = pending;
    }

    private static void ConsumeConsecutiveFrame(CanFrame frame, Dictionary<string, ActiveMessage> active, string streamKey, List<IsoTpMessage> messages, List<DiagnosticIssue> issues)
    {
        if (!active.TryGetValue(streamKey, out var pending))
        {
            issues.Add(Issue(frame, IssueSeverity.Error, "Unexpected consecutive frame", "A CF was received without a matching first frame on this CAN ID/direction stream."));
            return;
        }

        var sequenceNumber = frame.Data[0] & 0x0F;
        if (sequenceNumber != pending.ExpectedSequenceNumber)
        {
            var description = $"Expected CF sequence 0x{pending.ExpectedSequenceNumber:X1} but received 0x{sequenceNumber:X1}. Missing or reordered CAN frames are likely.";
            pending.Errors.Add(description);
            messages.Add(pending.ToMessage(IsoTpMessageStatus.Error, description, frame));
            issues.Add(Issue(frame, IssueSeverity.Error, "Wrong ISO-TP sequence number", description));
            active.Remove(streamKey);
            return;
        }

        pending.Payload.AddRange(frame.Data.Skip(1).Take(pending.ExpectedLength - pending.Payload.Count));
        pending.EndTimeMs = frame.TimestampMs;
        pending.EndLineNumber = frame.LineNumber;
        pending.ExpectedSequenceNumber = (pending.ExpectedSequenceNumber + 1) & 0x0F;

        if (pending.Payload.Count >= pending.ExpectedLength)
        {
            messages.Add(pending.ToMessage(IsoTpMessageStatus.Complete));
            active.Remove(streamKey);
        }
    }

    private static void CloseActiveAsIncomplete(Dictionary<string, ActiveMessage> active, string streamKey, CanFrame currentFrame, List<IsoTpMessage> messages, List<DiagnosticIssue> issues, string reason)
    {
        if (!active.Remove(streamKey, out var pending))
        {
            return;
        }

        messages.Add(pending.ToMessage(IsoTpMessageStatus.Incomplete, reason, currentFrame));
        issues.Add(Issue(currentFrame, IssueSeverity.Error, "Incomplete ISO-TP message", $"{reason} Expected {pending.ExpectedLength} bytes but received {pending.Payload.Count}."));
    }

    private static DiagnosticIssue Issue(CanFrame frame, IssueSeverity severity, string title, string description) => new()
    {
        Title = title,
        Description = description,
        Severity = severity,
        LineNumber = frame.LineNumber,
        TimestampMs = frame.TimestampMs,
        Channel = frame.CanId
    };

    private sealed class ActiveMessage
    {
        public ActiveMessage(CanFrame firstFrame, int expectedLength)
        {
            CanId = firstFrame.CanId;
            CanIdValue = firstFrame.CanIdValue;
            IsRx = firstFrame.IsRx;
            Bus = firstFrame.Bus;
            StartTimeMs = firstFrame.TimestampMs;
            EndTimeMs = firstFrame.TimestampMs;
            StartLineNumber = firstFrame.LineNumber;
            EndLineNumber = firstFrame.LineNumber;
            ExpectedLength = expectedLength;
        }

        public string CanId { get; }
        public uint CanIdValue { get; }
        public bool IsRx { get; }
        public int? Bus { get; }
        public double StartTimeMs { get; }
        public double EndTimeMs { get; set; }
        public int StartLineNumber { get; }
        public int EndLineNumber { get; set; }
        public int ExpectedLength { get; }
        public int ExpectedSequenceNumber { get; set; } = 1;
        public List<byte> Payload { get; } = [];
        public List<string> Errors { get; } = [];

        public IsoTpMessage ToMessage(IsoTpMessageStatus status, string? error = null, CanFrame? endFrame = null)
        {
            var errors = new List<string>(Errors);
            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add(error);
            }

            return new IsoTpMessage
            {
                CanId = CanId,
                CanIdValue = CanIdValue,
                IsRx = IsRx,
                Bus = Bus,
                StartTimeMs = StartTimeMs,
                EndTimeMs = endFrame?.TimestampMs ?? EndTimeMs,
                StartLineNumber = StartLineNumber,
                EndLineNumber = endFrame?.LineNumber ?? EndLineNumber,
                FrameType = IsoTpFrameType.FirstFrame,
                Status = status,
                Payload = Payload.Take(ExpectedLength).ToArray(),
                Errors = errors
            };
        }
    }
}
