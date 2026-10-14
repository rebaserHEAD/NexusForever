using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Trade;

namespace NexusForever.Game.Trade
{
    public class TradeParticipant : ITradeParticipant
    {
        public IPlayer Player { get; private set; }
        public bool IsInitiator { get; private set; }

        public List<TradeItem> Items { get; } = [];
        public ulong Credits { get; set; }
        public bool Committed { get; set; }

        /// <summary>
        /// Returns if the participant has nothing on offer.
        /// </summary>
        public bool IsOfferEmpty => Items.Count == 0 && Credits == 0ul;

        /// <summary>
        /// Initialise <see cref="ITradeParticipant"/> for the supplied <see cref="IPlayer"/>.
        /// </summary>
        public void Initialise(IPlayer player, bool isInitiator)
        {
            if (Player != null)
                throw new InvalidOperationException();

            Player      = player;
            IsInitiator = isInitiator;
        }
    }
}
