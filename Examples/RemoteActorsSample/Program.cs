using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks;
using Stacks.Actors;
using Stacks.Actors.Remote;

namespace RemoteActorsSample
{
  
    class Program
    {
        static void Main(string[] args)
        {
            var actorServer = ActorServerProxy.Create<CalculatorActor>("tcp://*:4632");

            ICalculatorActor calculator = ActorClientProxy.Create<ICalculatorActor>("tcp://127.0.0.1:4632").Result;

            Console.WriteLine("Result is: " + calculator.Add(5, 4).Result);

            Console.Write("Press any key to exit... ");
            Console.ReadKey();
        }
    }
}
