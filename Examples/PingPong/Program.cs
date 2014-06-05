using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using System.Reactive.Linq;
using Stacks.Tcp;

namespace PingPong
{
    class Program
    {
        static IFramedClient client;
        static IFramedClient serverClient;
        static Encoding encoding;

        static void Main(string[] args)
        {
            encoding = Encoding.ASCII;
            SocketServer server;
            server = new SocketServer(new IPEndPoint(IPAddress.Loopback, 0));

            server.Connected += c =>
                {
                    serverClient = new FramedClient(c);

                    // When received is called, bs will contain no more and no less
                    // data than whole packet as sent from client.
                    serverClient.Received.Subscribe(bs =>
                        {
                            var msg = encoding.GetString(bs.Array, bs.Offset, bs.Count);
                            msg = "Hello, " + msg + "!";
                            serverClient.SendPacket(encoding.GetBytes(msg));
                        });
                };

            server.Start();
            HandleClient(server.BindEndPoint.Port);
            
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            server.Stop();
            client.Close();
            serverClient.Close();
        }

        static async void HandleClient(int serverPort)
        {
            client = new FramedClient(
                            new SocketClient());

            client.Received.Subscribe(bs =>
                {
                    Console.WriteLine("Received: " +
                        encoding.GetString(bs.Array, bs.Offset, bs.Count));
                });
            
            await client.Connect(new IPEndPoint(IPAddress.Loopback, serverPort));
            client.SendPacket(encoding.GetBytes("Steve"));
        }
    }
}
