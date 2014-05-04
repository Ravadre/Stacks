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

            client.Connect(new IPEndPoint(IPAddress.Loopback, serverPort))
                  .ContinueWith(t =>
                  {
                      Console.WriteLine("Querying for temperature in London, Warsaw, Madrid");
                      client.Send(1, new TemperatureRequest { City = "London" });
                      client.Send(1, new TemperatureRequest { City = "Warsaw" });
                      client.Send(1, new TemperatureRequest { City = "Madrid" });
                  });
        }
    }

    public class ClientMessageHandler : IMessageHandler
    {
        [MessageHandler(2)]
        public void HandleTemperatureResponse(TemperatureResponse response)
        {
            Console.WriteLine("Received temperature response: " + 
                response.City + " = " + response.Temperature);
        }

        [MessageHandler(3)]
        public void HandleTemperatureChanged(TemperatureChanged tempChanged)
        {
            Console.WriteLine("Temperature changed: " +
                tempChanged.City + " = " + tempChanged.Temperature);
        }
    }
}
