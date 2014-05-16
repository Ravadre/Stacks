using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;
using Stacks;
using System.Net;

namespace ProtobufSample
{
    public class Client
    {
        MessageClient client;
        
        public Client()
        {
         
        }

        public void Run(int serverPort)
        {
            client = new MessageClient(
                     new FramedClient(new SocketClient()),
                     new ProtoBufStacksSerializer(),
                     new ClientMessageHandler());

            client.PreLoadTypesFromAssemblyOfType<TemperatureResponse>();

            client.Connect(new IPEndPoint(IPAddress.Loopback, serverPort))
                  .ContinueWith(t =>
                  {
                      Console.WriteLine("Querying for temperature in London, Warsaw, Madrid");
                      client.Send(new TemperatureRequest { City = "London" });
                      client.Send(new TemperatureRequest { City = "Warsaw" });
                      client.Send(new TemperatureRequest { City = "Madrid" });
                  });
        }
    }

    public class ClientMessageHandler : IMessageHandler
    {
        public void HandleTemperatureResponse(IMessageClient client, TemperatureResponse response)
        {
            Console.WriteLine("Received temperature response: " + 
                response.City + " = " + response.Temperature.ToString("F2") + "\u00b0C");
        }

        public void HandleTemperatureChanged(IMessageClient client, TemperatureChanged tempChanged)
        {
            Console.WriteLine("Temperature changed: " +
                tempChanged.City + " = " + tempChanged.Temperature.ToString("F2") + "\u00b0C");
        }
    }
}
