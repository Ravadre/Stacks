using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Stacks;
using Stacks.Tcp;
using Stacks.Actors;
using System.Net;

using System.Reactive.Linq;
using System.IO;
using ProtoBuf;

namespace RemoteActorsSample
{
    [ProtoContract]
    public class AddMessage
    {
        [ProtoMember(1)]
        public double x;
        [ProtoMember(2)]
        public double y;
    }

    [ProtoContract]
    public class AddMessageReply : IReplyMessage<double>
    {
        [ProtoMember(1)]
        public double Return;
        public double GetResult()
        {
            return this.Return;
        }
    }

    public class Server
    {
        private SocketServer server;
        private ProtoBufStacksSerializer serializer;

        public Server()
        {
            serializer = new ProtoBufStacksSerializer();
            server = new SocketServer("tcp://*:4632");
            server.Connected.Subscribe(sc =>
                {
                    var c = new FramedClient(sc);

                    c.Received.Subscribe(bs =>
                        {
                            unsafe
                            {
                                fixed (byte* b = bs.Array)
                                {
                                    byte* s = (b + bs.Offset);
                                    long reqId = *(long*)s;
                                    int hSize = *(int*)(s + 8);
                                    string msgName = Encoding.ASCII.GetString(bs.Array, bs.Offset + 12, hSize);

                                    Console.WriteLine("Received packet. Request: {0}. Msg: {1}", reqId, msgName);

                                    using (var ms = new MemoryStream(bs.Array, bs.Offset + 12 + hSize, bs.Count - 12 - hSize))
                                    {
                                        var addMsg = serializer.Deserialize<AddMessage>(ms);
                                        var result = addMsg.x + addMsg.y;

                                        using (var resp = new MemoryStream())
                                        {
                                            resp.SetLength(8);
                                            resp.Position = 8;
                                            serializer.Serialize<AddMessageReply>(new AddMessageReply() { Return = result }, resp);

                                            resp.Position = 0;
                                            resp.Write(BitConverter.GetBytes(reqId), 0, 8);
                                            resp.Position = 0;

                                            c.SendPacket(new ArraySegment<byte>(resp.GetBuffer(), 0, (int)resp.Length));
                                        }
                                    }
                                }
                            }


                        });
                });

            server.Start();
            server.Started.Wait();
        }
    }
}
