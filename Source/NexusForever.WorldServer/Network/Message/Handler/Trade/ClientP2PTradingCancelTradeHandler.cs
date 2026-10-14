using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingCancelTradeHandler : IMessageHandler<IWorldSession, ClientP2PTradingCancelTrade>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingCancelTradeHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player cancelling their trade.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingCancelTrade cancelTrade)
        {
            tradeManager.CancelTrade(session.Player);
        }
    }
}
