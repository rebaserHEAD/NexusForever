using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    [Message(GameMessageOpcode.ServerP2PTradeUpdateMoney)]
    public class ServerP2PTradeUpdateMoney : IWritable
    {
        public uint UnitId { get; set; } // UnitId of whichever trade partner updated their money offer
        public ulong Credits { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(UnitId);
            writer.Write(Credits);
        }
    }
}
