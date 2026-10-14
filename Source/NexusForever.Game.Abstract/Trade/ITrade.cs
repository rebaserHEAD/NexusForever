using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Trade;
using NexusForever.Network.Message;

namespace NexusForever.Game.Abstract.Trade
{
    public interface ITrade
    {
        ITradeParticipant Initiator { get; }
        ITradeParticipant Target { get; }
        TradeState State { get; }

        long CreatedTimestamp { get; }
        long LastChangeTimestamp { get; }
        long StateTimestamp { get; }

        List<IDisposable> SaveHolds { get; }
        List<TradeItemMove> ItemMoves { get; }
        Task SaveTask { get; set; }
        bool ExecutingWarned { get; set; }

        /// <summary>
        /// Initialise <see cref="ITrade"/> between the supplied initiator and target.
        /// </summary>
        void Initialise(IPlayer initiator, IPlayer target);

        /// <summary>
        /// Return both participants, initiator first.
        /// </summary>
        IEnumerable<ITradeParticipant> GetParticipants();

        /// <summary>
        /// Return the <see cref="ITradeParticipant"/> for the supplied <see cref="IPlayer"/>.
        /// </summary>
        ITradeParticipant GetParticipant(IPlayer player);

        /// <summary>
        /// Return the other <see cref="ITradeParticipant"/> to the supplied one.
        /// </summary>
        ITradeParticipant GetPartner(ITradeParticipant participant);

        /// <summary>
        /// Return the next unused index for an offered item.
        /// </summary>
        uint NextTradeIndex();

        /// <summary>
        /// Set the state and record when it was entered.
        /// </summary>
        void SetState(TradeState state);

        /// <summary>
        /// Clear both commits and record the time, after any change to either offer.
        /// </summary>
        void OnOfferChanged();

        /// <summary>
        /// Send the supplied message to both participants, initiator first.
        /// </summary>
        void Send(IWritable message);
    }
}
