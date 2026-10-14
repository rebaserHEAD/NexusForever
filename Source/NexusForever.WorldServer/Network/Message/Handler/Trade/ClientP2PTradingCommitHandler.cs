using NexusForever.Game.Abstract.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Trade
{
    public class ClientP2PTradingCommitHandler : IMessageHandler<IWorldSession, ClientP2PTradingCommit>
    {
        #region Dependency Injection

        private readonly ITradeManager tradeManager;

        public ClientP2PTradingCommitHandler(
            ITradeManager tradeManager)
        {
            this.tradeManager = tradeManager;
        }

        #endregion

        /// <summary>
        /// Handle the player committing to their trade, or withdrawing their commit.
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientP2PTradingCommit commit)
        {
            tradeManager.Commit(session.Player);
        }
    }
}
