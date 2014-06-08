using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks;

namespace ReactiveSample
{
    [ProtoContract]
    [StacksMessage(1)]
    public class RegisterSymbolRequest
    {
        [ProtoMember(1)]
        public string Symbol { get; set; }
        [ProtoMember(2)]
        public bool Register { get; set; }
    }

    [ProtoContract]
    [StacksMessage(2)]
    public class Price
    {
        [ProtoMember(1)]
        public string Symbol { get; set; }
        [ProtoMember(2)]
        public double Bid { get; set; }
        [ProtoMember(3)]
        public double Offer { get; set; }
    }

    public interface IServerPacketHandler
    {
        IObservable<RegisterSymbolRequest> RegisterSymbol { get; }
    }

    public interface IClientPacketHandler
    {
        IObservable<Price> Price { get; }
    }
}
