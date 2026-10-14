namespace NexusForever.Game.Static.Trade
{
    /// <summary>
    /// Stage of a player-to-player trade, from invite to exchange.
    /// </summary>
    public enum TradeState
    {
        /// <summary>
        /// Target has been invited and hasn't answered yet.
        /// </summary>
        Invited   = 0,
        /// <summary>
        /// Both players have the trade window open and can change their offers.
        /// </summary>
        Active    = 1,
        /// <summary>
        /// Both players have committed, the exchange waits for neither to have a save in progress.
        /// </summary>
        Committed = 2,
        /// <summary>
        /// Exchange has been applied and both characters are being written to the database.
        /// </summary>
        Executing = 3
    }
}
