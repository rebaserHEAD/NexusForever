using NexusForever.Game.Abstract.Entity;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Trade
{
    /// <summary>
    /// Holds every player-to-player trade from invite to exchange.
    /// </summary>
    /// <remarks>
    /// Must only be invoked on the world thread outside the map update: from the network update (packet handlers and logout) and from the world update after maps have updated.
    /// Map updates run in parallel and must never call into the trade manager.
    /// </remarks>
    public interface ITradeManager : IUpdate
    {
        /// <summary>
        /// Invoked when <see cref="IPlayer"/> logs out, ending any trade they are part of unless its exchange is being written.
        /// </summary>
        void OnLogout(IPlayer player);

        /// <summary>
        /// Invite the player with the supplied unit id to trade with <see cref="IPlayer"/>.
        /// </summary>
        void InitiateTrade(IPlayer player, uint targetUnitId);

        /// <summary>
        /// Accept the trade <see cref="IPlayer"/> has been invited to.
        /// </summary>
        void AcceptInvite(IPlayer player);

        /// <summary>
        /// Decline the trade <see cref="IPlayer"/> has been invited to.
        /// </summary>
        void DeclineInvite(IPlayer player);

        /// <summary>
        /// Cancel the trade <see cref="IPlayer"/> is part of.
        /// </summary>
        void CancelTrade(IPlayer player);

        /// <summary>
        /// Offer the item with the supplied guid from the inventory of <see cref="IPlayer"/>.
        /// </summary>
        void AddItem(IPlayer player, ulong itemGuid);

        /// <summary>
        /// Withdraw the offered item with the supplied guid.
        /// </summary>
        void RemoveItem(IPlayer player, ulong itemGuid);

        /// <summary>
        /// Set the amount of credits <see cref="IPlayer"/> offers.
        /// </summary>
        void SetMoney(IPlayer player, ulong credits);

        /// <summary>
        /// Commit <see cref="IPlayer"/> to the trade, or withdraw the commit if already committed.
        /// </summary>
        void Commit(IPlayer player);
    }
}
