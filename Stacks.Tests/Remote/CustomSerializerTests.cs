using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Stacks.Actors.Proto;
using Stacks.Actors.Remote;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class CustomSerializerTests
    {
        private IActorServerProxy server;
        private ISerializerActor client;


        [Fact]
        public async Task Using_custom_noop_serializer_should_succeed()
        {
            var opts = new ActorServerProxyOptions(false, s => new CustomNoopSerializer(s));
            Utils.CreateServerAndClient<SerializerActor, ISerializerActor>(opts, out server, out client);

            var res = await client.Add(5, 6);

            Assert.Equal(11, res);
        }
    }

    public interface ISerializerActor
    {
        Task<int> Add(int x, int y);
    }

    public class SerializerActor : ISerializerActor
    {
        public async Task<int> Add(int x, int y)
        {
            return x + y;
        }
    }

    public class CustomNoopSerializer : ActorPacketSerializer
    {
        private readonly IStacksSerializer serializer;

        public CustomNoopSerializer(IStacksSerializer serializer)
            : base(serializer)
        {
            this.serializer = serializer;
        }

        public override T Deserialize<T>(ActorProtocolFlags packetFlags, string requestName, MemoryStream ms)
        {
            return serializer.Deserialize<T>(ms);
        }

        public override void Serialize<T>(ActorProtocolFlags packetFlags, string requestName, T packet, MemoryStream ms)
        {
            serializer.Serialize(packet, ms);
        }
    }
}
