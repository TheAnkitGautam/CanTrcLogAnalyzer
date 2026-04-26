using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class TransactionMatcher(IEcuChannelResolver channelResolver) : ITransactionMatcher
{
    private const double ResponseTimeoutMs = 1000.0;

    public IReadOnlyList<UdsTransaction> Match(IReadOnlyList<IsoTpMessage> messages)
    {
        var transactions = new List<UdsTransaction>();
        var pending = new List<IsoTpMessage>();

        foreach (var message in messages
                     .Where(m => m.Status != IsoTpMessageStatus.FlowControl)
                     .OrderBy(m => m.StartTimeMs)
                     .ThenBy(m => m.StartLineNumber))
        {
            // 🚫 Skip non-UDS (e.g., J1939 like 1CFFxxxx)
            if (IsNonUds(message.CanIdValue))
            {
                message.Channel = channelResolver.ResolveForMessage(message);

                transactions.Add(new UdsTransaction
                {
                    Channel = message.Channel,
                    Response = message,
                    Status = UdsTransactionStatus.UnmatchedResponse
                });
                continue;
            }

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

            if (IsRequest(message))
            {
                message.Channel = channelResolver.ResolveForMessage(message);
                ExpireOldRequests(message.StartTimeMs, pending, transactions);
                pending.Add(message);
                continue;
            }

            var request = FindMatchingRequest(message, pending);
            message.Channel = channelResolver.ResolveForMessage(message, request);

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

            request.Channel = channelResolver.ResolveForMessage(request);
            pending.Remove(request);

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
        var id = message.CanIdValue;

        if (IsNonUds(id))
            return false;

        // 11-bit
        if (!IsExtended(id))
        {
            if (id == 0x7DF || (id >= 0x7E0 && id <= 0x7E7))
                return true;

            if (id >= 0x7E8 && id <= 0x7EF)
                return false;
        }
        else
        {
            if (IsFunctional(id))
                return true;

            // fallback (robust for unknown tester IDs)
            return !message.IsRx;
        }

        return !message.IsRx;
    }

    // ============================
    // 🔍 Matching Logic
    // ============================
    private static IsoTpMessage? FindMatchingRequest(IsoTpMessage response, List<IsoTpMessage> pending)
    {
        var responseOriginalSid = response.Decoded?.OriginalServiceId;

        return pending
            .Where(request => request.Bus == response.Bus)
            .Where(request => response.StartTimeMs >= request.StartTimeMs &&
                              response.StartTimeMs - request.StartTimeMs <= ResponseTimeoutMs)
            .Where(request =>
                IsPhysicalPair(request.CanIdValue, response.CanIdValue) ||
                IsFunctional(request.CanIdValue))
            .Where(request =>
                responseOriginalSid is null ||
                request.Decoded?.ServiceId == responseOriginalSid)
            .OrderByDescending(request => request.StartTimeMs)
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

        // 29-bit UDS (address swap logic)
        if (IsUds29Bit(requestId) && IsUds29Bit(responseId))
        {
            var reqSrc = GetSource(requestId);
            var reqTgt = GetTarget(requestId);

            var resSrc = GetSource(responseId);
            var resTgt = GetTarget(responseId);

            return reqSrc == resTgt && reqTgt == resSrc;
        }

        return false;
    }

    // ============================
    // 🔧 Timeout Handling
    // ============================
    private void ExpireOldRequests(double nowMs, List<IsoTpMessage> pending, List<UdsTransaction> transactions)
    {
        foreach (var request in pending.Where(r => nowMs - r.StartTimeMs > ResponseTimeoutMs).ToList())
        {
            request.Channel ??= channelResolver.ResolveForMessage(request);

            transactions.Add(new UdsTransaction
            {
                Channel = request.Channel,
                Request = request,
                Status = UdsTransactionStatus.Timeout
            });

            pending.Remove(request);
        }
    }

    // ============================
    // 🧠 CAN ID HELPERS
    // ============================

    private static bool IsExtended(uint id) => id > 0x7FF;

    private static bool IsUds29Bit(uint id)
    {
        return (id & 0x1FFF0000) == 0x18DA0000 ||
               (id & 0x1FFF0000) == 0x18DB0000;
    }

    private static bool IsNonUds(uint id)
    {
        return IsExtended(id) && !IsUds29Bit(id);
    }

    private static bool IsFunctional(uint id)
    {
        if (!IsExtended(id))
            return id == 0x7DF;

        return (id & 0x1FFF0000) == 0x18DB0000;
    }

    private static byte GetSource(uint id) => (byte)((id >> 8) & 0xFF);

    private static byte GetTarget(uint id) => (byte)(id & 0xFF);
}