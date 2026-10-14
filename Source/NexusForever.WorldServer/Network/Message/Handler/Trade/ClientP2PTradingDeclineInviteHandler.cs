using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingDeclineInviteHandler : IMessageHandler<IWorldSession, ClientP2PTradingDeclineInvite>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingDeclineInviteHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player declining a trade invite.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingDeclineInvite declineInvite)
        {
            tradeManager.DeclineInvite(session.Player);
        }
    }
}
