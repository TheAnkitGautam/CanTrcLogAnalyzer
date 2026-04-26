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
                Status = message.Decoded?.IsNegativeResponse == true ? UdsTransactionStatus.NegativeResponse : UdsTransactionStatus.Success
            });
        }

        ExpireOldRequests(double.MaxValue, pending, transactions);
        return transactions;
    }

    private static bool IsRequest(IsoTpMessage message)
    {
        if (message.CanIdValue == 0x7DF || message.CanIdValue is >= 0x7E0 and <= 0x7E7)
        {
            return true;
        }

        if (message.CanIdValue is >= 0x7E8 and <= 0x7EF)
        {
            return false;
        }

        return !message.IsRx;
    }

    private static IsoTpMessage? FindMatchingRequest(IsoTpMessage response, List<IsoTpMessage> pending)
    {
        var responseOriginalSid = response.Decoded?.OriginalServiceId;
        return pending
            .Where(request => request.Bus == response.Bus)
            .Where(request => response.StartTimeMs >= request.StartTimeMs && response.StartTimeMs - request.StartTimeMs <= ResponseTimeoutMs)
            .Where(request => IsPhysicalPair(request.CanIdValue, response.CanIdValue) || request.CanIdValue == 0x7DF)
            .Where(request => responseOriginalSid is null || request.Decoded?.ServiceId == responseOriginalSid)
            .OrderByDescending(request => request.StartTimeMs)
            .FirstOrDefault();
    }

    private static bool IsPhysicalPair(uint requestId, uint responseId) =>
        requestId is >= 0x7E0 and <= 0x7E7 && responseId == requestId + 8;

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
}
