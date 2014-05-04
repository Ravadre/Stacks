using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Tcp;

namespace PingPong
{
    class Program
    {
        static IFramedClient client;

        static void Main(string[] args)
        {
            SocketServer server;
            server = new SocketServer(new IPEndPoint(IPAddress.Loopback, 0));

            server.Started += () =>
                {
                    HandleClient(server.BindEndPoint.Port);
                };
            server.Connected += c =>
                {
                    var framed = new FramedClient(c);
                    framed.Received += bs =>
                        {
                            var msg = Encoding.ASCII.GetString(bs.Array, bs.Offset, bs.Count);
                            msg = "Hello, " + msg + "!";
                            framed.SendPacket(Encoding.ASCII.GetBytes(msg));
                        };
                };
            server.Start();

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            server.Stop();
            client.Close();

        }

        static async void HandleClient(int serverPort)
        {
            client = new FramedClient(
                            new SocketClient());

            client.Received += bs =>
                {
                    Console.WriteLine("Received: " + 
                        Encoding.ASCII.GetString(bs.Array, bs.Offset, bs.Count));
                };
            
            await client.Connect(new IPEndPoint(IPAddress.Loopback, serverPort));
            client.SendPacket(Encoding.ASCII.GetBytes("Steve"));
        }
    }

    class Server
    {
    }

    class Client
    {
        public void Run()
        {

        }
    }
}
