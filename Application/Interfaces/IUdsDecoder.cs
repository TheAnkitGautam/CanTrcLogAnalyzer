using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface IUdsDecoder
{
    UdsDecodedMessage? Decode(IsoTpMessage message);
}
