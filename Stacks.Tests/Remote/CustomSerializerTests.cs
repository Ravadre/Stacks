using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        [Fact]
        public async Task Using_custom_serializer_it_should_be_possible_to_tamper_packets()
        {
            var opts = new ActorServerProxyOptions(false, s => new CustomTamperSerializer(s));
            var cOpts = new ActorClientProxyOptions(s => new CustomTamperSerializer(s));
            Utils.CreateServerAndClient<SerializerActor, ISerializerActor>(opts, cOpts, out server, out client);

            var res = await client.Add(5, 6);

            Assert.Equal(11, res);
        }

        [Fact]
        public async Task Using_encrypt_serializer_should_be_possible_to_tamper_packets()
        {
            var opts = new ActorServerProxyOptions(false, s => new EncryptSerializer(s));
            var cOpts = new ActorClientProxyOptions(s => new EncryptSerializer(s));
            Utils.CreateServerAndClient<SerializerActor, ISerializerActor>(opts, cOpts, out server, out client);

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


    public class CustomTamperSerializer : ActorPacketSerializer
    {
        private readonly IStacksSerializer serializer;

        public CustomTamperSerializer(IStacksSerializer serializer)
            : base(serializer)
        {
            this.serializer = serializer;
        }

        public override T Deserialize<T>(ActorProtocolFlags packetFlags, string requestName, MemoryStream ms)
        {
            ms.Position += 1;
            return serializer.Deserialize<T>(ms);
        }

        public override void Serialize<T>(ActorProtocolFlags packetFlags, string requestName, T packet, MemoryStream ms)
        {
            ms.WriteByte(5);
            serializer.Serialize(packet, ms);
        }
    }


    public class EncryptSerializer : ActorPacketSerializer
    {
        private readonly IStacksSerializer serializer;

        public EncryptSerializer(IStacksSerializer serializer)
            : base(serializer)
        {
            this.serializer = serializer;
        }

        public override T Deserialize<T>(ActorProtocolFlags packetFlags, string requestName, MemoryStream ms)
        {
            //var data = serializer.Deserialize<T>(ms);
            byte[] data = new byte[ms.Length];
            ms.Read(data, 0, (int)ms.Length);

           using (var rijndael = new RijndaelManaged())
           {
               rijndael.Key = Encoding.ASCII.GetBytes("1234567890123456");
               rijndael.IV = Encoding.ASCII.GetBytes("1234567890123456");

               var outData = rijndael.CreateDecryptor().TransformFinalBlock(data, 0, data.Length);

               var decMs = new MemoryStream();
               decMs.Write(outData, 0, outData.Length);
               decMs.Position = 0;
               return serializer.Deserialize<T>(decMs);
           }
        }

        public override void Serialize<T>(ActorProtocolFlags packetFlags, string requestName, T packet, MemoryStream ms)
        {
            var decMs = new MemoryStream();
            serializer.Serialize(packet, decMs);

            byte[] data = new byte[decMs.Length];
            decMs.Read(data, 0, (int)decMs.Length);

            using (var rijndael = new RijndaelManaged())
            {
                rijndael.Key = Encoding.ASCII.GetBytes("1234567890123456");
                rijndael.IV = Encoding.ASCII.GetBytes("1234567890123456");

                var outData = rijndael.CreateEncryptor().TransformFinalBlock(data, 0, data.Length);

                ms.Write(outData, 0, outData.Length);
            }
        }
    }
}
