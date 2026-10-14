using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingRemoveItemHandler : IMessageHandler<IWorldSession, ClientP2PTradingRemoveItem>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingRemoveItemHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player withdrawing an offered item from their trade.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingRemoveItem removeItem)
        {
            tradeManager.RemoveItem(session.Player, removeItem.ItemGuid);
        }
    }
}
