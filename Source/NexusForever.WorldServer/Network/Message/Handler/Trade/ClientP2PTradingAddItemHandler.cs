using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingAddItemHandler : IMessageHandler<IWorldSession, ClientP2PTradingAddItem>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingAddItemHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player offering an item in their trade.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingAddItem addItem)
        {
            tradeManager.AddItem(session.Player, addItem.ItemGuid);
        }
    }
}
