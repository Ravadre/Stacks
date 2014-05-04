using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Actors;
using Stacks.Tcp;

namespace ProtobufSample
{
    public class Server
    {
        SocketServer server;
        MessageClient client;
        TemperatureService service;

        public int ServerPort { get; private set; }

        public Server()
        {

        }

        public void Run()
        {
            service = new TemperatureService();
            server = new SocketServer(new IPEndPoint(IPAddress.Loopback, 0));

            server.Connected += ClientConnected;

            server.Start();
            this.ServerPort = server.BindEndPoint.Port;

            service.TemperatureChanged += TempChanged;
        }

        private void TempChanged(string city, double temp)
        {
            if (this.client != null)
            {
                this.client.Send(3, new TemperatureChanged
                {
                    City = city,
                    Temperature = temp
                });
            }
        }

        private void ClientConnected(SocketClient client)
        {
            this.client = new MessageClient(
                            new FramedClient(client),
                            new ProtoBufStacksSerializer(),
                            new ServerMessageHandler(service));
        }
    }

    public class ServerMessageHandler : Actor, IMessageHandler
    {
        TemperatureService service;

        public ServerMessageHandler(TemperatureService service)
        {
            this.service = service;
        }

        [MessageHandler(1)]
        public async void HandleTemperatureRequest(MessageClient client, TemperatureRequest request)
        {
            await Context;

            var temp = await service.GetTemperature(request.City);

            client.Send(2, new TemperatureResponse
            {
                City = request.City,
                Temperature = temp,
            });
        }
    }

    public class TemperatureService : Actor
    {
        private Dictionary<string, double> temps;
        private Random rng;

        public event Action<string, double> TemperatureChanged;

        public TemperatureService()
        {
            rng = new Random();
            temps = new Dictionary<string, double>()
            {
                { "London", 23 },
                { "Warsaw", 25 }
            };


            Task.Run(new Action(async () =>
                {
                    await Context;

                    while (true)
                    {
                        await Task.Delay(1000);
                        temps["London"] += rng.NextDouble() - 0.5;
                        temps["Warsaw"] += rng.NextDouble() - 0.5;

                        var h = TemperatureChanged;
                        if (h != null)
                            h("London", temps["London"]);
                    }
                }));
        }

        public async Task<double> GetTemperature(string city)
        {
            await Context;

            if (temps.ContainsKey(city))
                return temps[city];
            else
                return 0;
        }
    }
}
