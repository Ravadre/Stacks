using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks;
using Stacks.Actors;

namespace RemoteActorsSample
{
  
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server();

            ICalculatorActor calculator = ActorClientProxy.Create<ICalculatorActor>("tcp://127.0.0.1:4632");
            Console.WriteLine("Result is: " + calculator.Add(5, 4).Result);

            Console.Write("Press any key to exit... ");
            Console.ReadKey();
        }
    }
}
