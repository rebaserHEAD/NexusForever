namespace NexusForever.Game.Abstract.Trade
{
    /// <summary>
    /// An item offered in a trade, as it was when offered.
    /// </summary>
    public readonly record struct TradeItem(uint TradeIndex, ulong Guid, uint Item2Id, uint Quantity);
}
