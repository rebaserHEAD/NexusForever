using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingInitiateTradeHandler : IMessageHandler<IWorldSession, ClientP2PTradingInitiateTrade>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingInitiateTradeHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player asking another player to trade.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingInitiateTrade initiateTrade)
        {
            tradeManager.InitiateTrade(session.Player, initiateTrade.TargetUnitId);
        }
    }
}
