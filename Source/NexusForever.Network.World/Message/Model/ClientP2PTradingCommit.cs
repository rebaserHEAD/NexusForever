using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    [Message(GameMessageOpcode.ClientP2PTradingCommit)]
    public class ClientP2PTradingCommit : IReadable
    {
        // Zero byte message
        //
        // The same message commits to the trade and withdraws the commit again.
        // Cancelling a committed trade from the UI clears the money offer, sends this and then ClientP2PTradingCancelTrade.
        public void Read(GamePacketReader reader)
        {
        }
    }
}
