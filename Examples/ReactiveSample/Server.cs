using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Stacks.Tcp;
using System.Reactive.Linq;
using Stacks;

namespace ReactiveSample
{
    public class Server
    {
        SocketServer server;
        
        MarketService service;

        public int ServerPort { get; private set; }

        public Server()
        {

        }

        public void Run()
        {
            ReactiveMessageClient<IServerPacketHandler> client = null;

            service = new MarketService();
            server = new SocketServer(new IPEndPoint(IPAddress.Loopback, 0));

            server.Connected.Subscribe(c =>
                {
                    client = new ReactiveMessageClient<IServerPacketHandler>(
                                    new FramedClient(c),
                                    new ProtoBufStacksSerializer());
                    client.PreLoadTypesFromAssemblyOfType<Price>();

                    client.Packets.RegisterSymbol.Subscribe(req =>
                        {
                            if (req.Register)
                                service.RegisterSymbol(req.Symbol);
                            else
                                service.UnregisterSymbol(req.Symbol);
                        });
                });

            service.PricesChanged.Subscribe(prices =>
                {
                    Console.WriteLine("Services reported that prices changed");

                    foreach (var p in prices)
                    {
                        if (client != null)
                            client.Send(p);
                    }
                });

            server.Start();
            this.ServerPort = server.BindEndPoint.Port;
        }
    }

    public class MarketService : Actor
    {
        private HashSet<string> registeredSymbols;
        private List<Price> prices;
        private Random rng;

        public IObservable<Price[]> PricesChanged { get; private set; }

        public MarketService()
        {
            prices = new List<Price>
            {
                new Price { Symbol = "EURUSD", Bid = 1.34215, Offer = 1.34241 },
                new Price { Symbol = "GBPUSD", Bid = 1.68064, Offer = 1.68073 },
                new Price { Symbol = "GBPLN", Bid = 5.04360, Offer = 5.04385 },
            };

            registeredSymbols = new HashSet<string>();
            rng = new Random();

            PricesChanged = Observable.Interval(TimeSpan.FromSeconds(1.0), Context)
                                .Do(_ =>
                                    {
                                        foreach (var p in prices)
                                        {
                                            p.Bid += (rng.Next(10) - 4) * Math.Pow(10, -5);
                                            p.Offer += (rng.Next(10) - 4) * Math.Pow(10, -5);
                                        }
                                    })
                                .Select(_ => prices.Where(p => registeredSymbols.Contains(p.Symbol))
                                                  .ToArray())
                                .Where(p => p.Length > 0);
        }

        public async void RegisterSymbol(string symbol)
        {
            await Context;

            registeredSymbols.Add(symbol);
        }

        public async void UnregisterSymbol(string symbol)
        {
            await Context;

            registeredSymbols.Remove(symbol);
        }
    }
}
