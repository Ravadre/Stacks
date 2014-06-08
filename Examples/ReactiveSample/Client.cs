using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Tcp;

namespace ReactiveSample
{
    public class Client
    {
        ReactiveMessageClient<IClientPacketHandler> client;
        IDisposable priceObserver;

        public Client()
        {

        }

        public void Run(int serverPort)
        {
            client = new ReactiveMessageClient<IClientPacketHandler>(
                     new FramedClient(new SocketClient()),
                     new ProtoBufStacksSerializer());

            client.PreLoadTypesFromAssemblyOfType<Price>();

            priceObserver = client.Packets.Price.Subscribe(p =>
                                {
                                    Console.WriteLine("Price received: {0} - {1:F5} / {2:F5}", 
                                        p.Symbol, p.Bid, p.Offer);
                                });

            client.Connect(new IPEndPoint(IPAddress.Loopback, serverPort))
                  .Subscribe(async _ =>
                  {
                      await Task.Delay(2000);
                      client.Send(new RegisterSymbolRequest() { Symbol = "EURUSD", Register = true });
                      client.Send(new RegisterSymbolRequest() { Symbol = "GBPLN", Register = true });
                  });

        }
    }
}
