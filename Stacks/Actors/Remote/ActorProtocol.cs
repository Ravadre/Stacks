using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace Stacks.Actors.Proto
{
    [Flags]
    enum ActorProtocolFlags : int
    {
        RequestReponse = 0x01,
        Observable = 0x02,
        StacksProtocol = 0x04,
    }

    public static class ActorProtocol
    {
        public static readonly int Version = 1;

        public static readonly int HandshakeId = 1;
        public static readonly int PingId = 2;
    }

    [ProtoContract]
    class HandshakeRequest
    {
        [ProtoMember(1)]
        public int ClientProtocolVersion { get; set; }
    }

    [ProtoContract]
    class HandshakeResponse
    {
        [ProtoMember(1)]
        public int RequestedProtocolVersion { get; set; }
        [ProtoMember(2)]
        public int ServerProtocolVersion { get; set; }
        [ProtoMember(3)]
        public bool ProtocolMatch { get; set; }
    }

    [ProtoContract]
    class Ping
    {
        [ProtoMember(1)]
        public long Timestamp { get; set; }
    }
}

namespace Stacks.Actors
{
    [Serializable]
    public class InvalidProtocolException : Exception
    {
        public InvalidProtocolException() { }
        public InvalidProtocolException(string message) : base(message) { }
        public InvalidProtocolException(string message, Exception inner) : base(message, inner) { }
        protected InvalidProtocolException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
