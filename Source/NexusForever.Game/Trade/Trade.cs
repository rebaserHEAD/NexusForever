using System.Diagnostics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Trade;
using NexusForever.Game.Static.Trade;
using NexusForever.Network.Message;
using NexusForever.Shared;

namespace NexusForever.Game.Trade
{
    public class Trade : ITrade
    {
        private uint nextTradeIndex = 1u;

        public ITradeParticipant Initiator { get; private set; }
        public ITradeParticipant Target { get; private set; }
        public TradeState State { get; private set; } = TradeState.Invited;

        public long CreatedTimestamp { get; private set; }
        public long LastChangeTimestamp { get; private set; }
        public long StateTimestamp { get; private set; }

        public List<IDisposable> SaveHolds { get; } = [];
        public List<TradeItemMove> ItemMoves { get; } = [];
        public Task SaveTask { get; set; }
        public bool ExecutingWarned { get; set; }

        #region Dependency Injection

        private readonly IFactory<ITradeParticipant> participantFactory;

        public Trade(
            IFactory<ITradeParticipant> participantFactory)
        {
            this.participantFactory = participantFactory;
        }

        #endregion

        /// <summary>
        /// Initialise <see cref="ITrade"/> between the supplied initiator and target.
        /// </summary>
        public void Initialise(IPlayer initiator, IPlayer target)
        {
            if (Initiator != null)
                throw new InvalidOperationException();

            Initiator = participantFactory.Resolve();
            Initiator.Initialise(initiator, true);

            Target = participantFactory.Resolve();
            Target.Initialise(target, false);

            CreatedTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Return both participants, initiator first.
        /// </summary>
        public IEnumerable<ITradeParticipant> GetParticipants()
        {
            yield return Initiator;
            yield return Target;
        }

        /// <summary>
        /// Return the <see cref="ITradeParticipant"/> for the supplied <see cref="IPlayer"/>.
        /// </summary>
        public ITradeParticipant GetParticipant(IPlayer player)
        {
            if (Initiator.Player == player)
                return Initiator;

            if (Target.Player == player)
                return Target;

            return null;
        }

        /// <summary>
        /// Return the other <see cref="ITradeParticipant"/> to the supplied one.
        /// </summary>
        public ITradeParticipant GetPartner(ITradeParticipant participant)
        {
            return participant == Initiator ? Target : Initiator;
        }

        /// <summary>
        /// Return the next unused index for an offered item.
        /// </summary>
        public uint NextTradeIndex()
        {
            return nextTradeIndex++;
        }

        /// <summary>
        /// Set the state and record when it was entered.
        /// </summary>
        public void SetState(TradeState state)
        {
            State          = state;
            StateTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Clear both commits and record the time, after any change to either offer.
        /// </summary>
        public void OnOfferChanged()
        {
            Initiator.Committed = false;
            Target.Committed    = false;
            LastChangeTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Send the supplied message to both participants, initiator first.
        /// </summary>
        public void Send(IWritable message)
        {
            foreach (ITradeParticipant participant in GetParticipants())
                participant.Player.Session.EnqueueMessageEncrypted(message);
        }
    }
}
