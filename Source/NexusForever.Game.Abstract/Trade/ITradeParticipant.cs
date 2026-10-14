using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Trade
{
    public interface ITradeParticipant
    {
        IPlayer Player { get; }
        bool IsInitiator { get; }

        List<TradeItem> Items { get; }
        ulong Credits { get; set; }
        bool Committed { get; set; }

        /// <summary>
        /// Returns if the participant has nothing on offer.
        /// </summary>
        bool IsOfferEmpty { get; }

        /// <summary>
        /// Initialise <see cref="ITradeParticipant"/> for the supplied <see cref="IPlayer"/>.
        /// </summary>
        void Initialise(IPlayer player, bool isInitiator);
    }
}
