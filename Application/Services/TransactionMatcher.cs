using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;
using System.Runtime.Serialization;

namespace EvUdsAnalyzer.Application.Services;

public sealed class TransactionMatcher(IEcuChannelResolver channelResolver) : ITransactionMatcher
{
    private const double DefaultTimeoutMs = 1000.0;
    private const double ExtendedTimeoutMs = 5000.0;

    private readonly Dictionary<IsoTpMessage, double> _extendedDeadlines = new();
    private readonly HashSet<IsoTpMessage> _completedRequests = new();
    public IReadOnlyList<UdsTransaction> Match(IReadOnlyList<IsoTpMessage> messages)
    {
        var transactions = new List<UdsTransaction>();
        var pending = new List<IsoTpMessage>();

        foreach (var message in messages
                     .Where(m => m.Status != IsoTpMessageStatus.FlowControl)
                     .OrderBy(m => m.StartTimeMs)
                     .ThenBy(m => m.StartLineNumber))
        {
            // ISO-TP error
            if (message.Status != IsoTpMessageStatus.Complete)
            {
                message.Channel = channelResolver.ResolveForMessage(message);
                transactions.Add(new UdsTransaction
                {
                    Channel = message.Channel,
                    Request = IsRequest(message) ? message : null,
                    Response = IsRequest(message) ? null : message,
                    Status = UdsTransactionStatus.IsoTpError
                });
                continue;
            }

            // Request message
            if (IsRequest(message))
            {
                message.Channel = channelResolver.ResolveForMessage(message);
                ExpireOldRequests(message.StartTimeMs, pending, transactions);
                pending.Add(message);
                continue;
            }

            // response message
            var request = FindMatchingRequest(message, pending);
            message.Channel = channelResolver.ResolveForMessage(message, request);

            // No matching request found
            if (request is null)
            {
                transactions.Add(new UdsTransaction
                {
                    Channel = message.Channel,
                    Response = message,
                    Status = UdsTransactionStatus.UnmatchedResponse
                });
                continue;
            }

            // Ignore responses after request is already completed (can happen with multiple responses or interleaved messages)
            if (_completedRequests.Contains(request))
            {
                transactions.Add(new UdsTransaction
                {
                    Channel = message.Channel,
                    Request = request,
                    Response = message,
                    Status = UdsTransactionStatus.UnmatchedResponse
                });
                continue;
            }

            // NRC 0x78 (Response Pending) extends the response deadline for the request
            if (IsResponsePending(message))
            {
                _extendedDeadlines[request] = message.StartTimeMs + ExtendedTimeoutMs;
                transactions.Add(new UdsTransaction
                {
                    Channel = message.Channel,
                    Request = request,
                    Response = message,
                    Status = UdsTransactionStatus.NegativeResponse
                });
                // keep request pending for the next response
                continue;
            }
            // final response
            request.Channel = channelResolver.ResolveForMessage(request);
            pending.Remove(request);
            _extendedDeadlines.Remove(request);
            _completedRequests.Add(request);

            transactions.Add(new UdsTransaction
            {
                Channel = message.Channel,
                Request = request,
                Response = message,
                Status = message.Decoded?.IsNegativeResponse == true
                    ? UdsTransactionStatus.NegativeResponse
                    : UdsTransactionStatus.Success
            });
        }

        ExpireOldRequests(double.MaxValue, pending, transactions);
        return transactions;
    }

    // ============================
    // 🔍 Request Detection
    // ============================
    private static bool IsRequest(IsoTpMessage message)
    {
        var sid = message.Decoded?.ServiceId;

        if (sid == null)
            return !message.IsRx;

        return sid < 0x40;
    }

    // ============================
    // NRC 0x78 Detection
    // ============================
    private static bool IsResponsePending(IsoTpMessage message)
    {
        return message.Decoded?.IsNegativeResponse == true &&
               message.Decoded?.ServiceId == 0x78;
    }

    // ============================
    // 🔍 Matching Logic
    // ============================
    private IsoTpMessage? FindMatchingRequest(IsoTpMessage response, List<IsoTpMessage> pending)
    {
        var responseOriginalSid = response.Decoded?.OriginalServiceId;

        return pending
             .Where(req => !_completedRequests.Contains(req))
             .Where(req => req.Bus == response.Bus)
             .Where(req =>
                        response.StartTimeMs >= req.StartTimeMs &&
                        response.StartTimeMs - req.StartTimeMs <= 10000)
             .Where(req => IsPhysicalPair(req.CanIdValue, response.CanIdValue))
             .Where(req =>
                responseOriginalSid is null ||
                req.Decoded?.ServiceId == responseOriginalSid)
             .OrderBy(req => req.StartTimeMs)
             .FirstOrDefault();
    }

    // ============================
    // 🔧 Pairing Logic
    // ============================
    private static bool IsPhysicalPair(uint requestId, uint responseId)
    {
        // 11-bit
        if (!IsExtended(requestId) && !IsExtended(responseId))
        {
            return requestId is >= 0x7E0 and <= 0x7E7 &&
                   responseId == requestId + 8;
        }


        var reqSrc = GetSource(requestId);
        var reqTgt = GetTarget(requestId);

        var resSrc = GetSource(responseId);
        var resTgt = GetTarget(responseId);

        return reqSrc == resTgt && reqTgt == resSrc;
    }

    // ============================
    // 🔧 Timeout Handling
    // ============================
    private void ExpireOldRequests(double nowMs, List<IsoTpMessage> pending, List<UdsTransaction> transactions)
    {
        foreach (var request in pending.ToList())
        {
            var deadline = _extendedDeadlines.TryGetValue(request, out var ext)
                ? ext
                : request.StartTimeMs + DefaultTimeoutMs;

            if (nowMs <= deadline)
                continue;

            request.Channel ??= channelResolver.ResolveForMessage(request);

            transactions.Add(new UdsTransaction
            {
                Channel = request.Channel,
                Request = request,
                Status = UdsTransactionStatus.Timeout
            });

            pending.Remove(request);
            _extendedDeadlines.Remove(request);
            _completedRequests.Add(request);
        }
    }

    // ============================
    // HELPERS
    // ============================

    private static bool IsExtended(uint id) => id > 0x7FF;

    private static byte GetSource(uint id) => (byte)((id >> 8) & 0xFF);

    private static byte GetTarget(uint id) => (byte)(id & 0xFF);
}