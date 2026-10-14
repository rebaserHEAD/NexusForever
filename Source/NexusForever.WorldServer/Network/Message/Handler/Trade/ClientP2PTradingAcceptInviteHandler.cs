using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingAcceptInviteHandler : IMessageHandler<IWorldSession, ClientP2PTradingAcceptInvite>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingAcceptInviteHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player accepting a trade invite.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingAcceptInvite acceptInvite)
        {
            tradeManager.AcceptInvite(session.Player);
        }
    }
}
