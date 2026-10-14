using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Trade;
using NexusForever.Shared;

namespace NexusForever.Game.Trade
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register the player-to-player trade services.
        /// </summary>
        public static void AddGameTrade(this IServiceCollection sc)
        {
            sc.AddSingleton<ITradeManager, TradeManager>();

            sc.AddTransientFactory<ITrade, Trade>();
            sc.AddTransientFactory<ITradeParticipant, TradeParticipant>();
        }
    }
}
