using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Trade
{
    /// <summary>
    /// An item that changed hands when a trade's exchange was applied.
    /// </summary>
    public readonly record struct TradeItemMove(ITradeParticipant From, ITradeParticipant To, IItem Item);
}
