using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingSetMoneyHandler : IMessageHandler<IWorldSession, ClientP2PTradingSetMoney>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingSetMoneyHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player setting the credits they offer in their trade.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingSetMoney setMoney)
        {
            tradeManager.SetMoney(session.Player, setMoney.Credits);
        }
    }
}
