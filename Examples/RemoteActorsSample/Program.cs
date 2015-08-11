using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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
            // Note: In samples below, .Result is used to force eager evaluation, however,
            //       async / await patterns could be used as well.

            // Creating and starting server proxy for an actor is easy.
            // Returned reference can be used to stop server.
            var actorServer = ActorServerProxy.Create<ICalculatorActor, CalculatorActor>("tcp://*:4632");

            // To create client proxy and connect to the server just pass an interface which describes
            // what method actor supports. Returned object implements given interface,
            // so one can use one interface to call local and remote actors.
            // Returned task is signalled when actor successfully connects to the server.
            ICalculatorActor calculator = ActorClientProxy.CreateActor<ICalculatorActor>("tcp://localhost:4632").Result;

            {

                // Task will throw an exception if it won't be able to connect to the server.
                try
                {
                    ICalculatorActor wrongAddress = ActorClientProxy.CreateActor<ICalculatorActor>("tcp://127.0.0.1:45662").Result;
                }
                catch (AggregateException exc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Could not connect to server: " + exc.InnerException.Message);
                }
            }

            {
                // Messages can be sent just by calling methods.
                // Returned tasks will be notified when network response will be received
                // from server.
                Console.WriteLine();
                Console.WriteLine("5 + 4 = " + calculator.Add(5, 4).Result);
            }



            {
                // Primitive types are serialized automatically, if custom classes are to be used
                // they should be implemented with ProtoContract and ProtoMember attributes,
                // so that Protobuf-net will know how to serialize them.
                // Complex objects can be used both as parameters and results.
                var rectInfo = calculator.CalculateRectangle(new Rectangle { A = 5, B = 6 }).Result;

                Console.WriteLine();
                Console.WriteLine("Recangle 5 x 6 has field equal = {0} and perimeter = {1}",
                    rectInfo.Field, rectInfo.Perimeter);
            }



            {
                // Because actor called on the server side is always the same instance
                // (until client has been reconnected, which gives no warranty on state of server actor)
                // it can be used to persist state just as local actor can.

                // Note: You don't need to wait for task to finish, messages are invoked on the server
                // in the order they were called by client proxy.
                calculator.PushNumber(12.0);
                calculator.PushNumber(15.0);

                var x = calculator.PopNumber();
                var y = calculator.PopNumber();

                Console.WriteLine();
                Console.WriteLine("15 - 12 = " + calculator.Subtract(x.Result, y.Result).Result);
            }



            {
                // Any errors will be serialized and sent back to the client proxy.
                // No number on stack, should throw.
                // Note, that Task aggregates exception, however, when using async/await pattern
                // they will be automatically unwrapped.
                var error = calculator.PopNumber();

                try
                {
                    error.Wait();
                }
                catch (AggregateException exc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error while popping from stack: " + exc.InnerException.Message);
                }
            }

            // Error on server side will by default stop actor. So we need to restart it.
            actorServer.Stop();
            actorServer = ActorServerProxy.Create<ICalculatorActor, CalculatorActor>("tcp://*:4632");
            calculator = ActorClientProxy.CreateActor<ICalculatorActor>("tcp://localhost:4632").Result;


            {
                // Remote actor still acts as an actor when it comes to message execution, which is serial.
                // This means, as with local actors, you can queue messages safetly, without worrying
                // about order execution and multithreading errors.
                Console.WriteLine();
                Console.WriteLine("Pinging...");

                var p1 = calculator.Ping();
                var p2 = calculator.Ping();
                var p3 = calculator.Ping();

                Task.WhenAll(p1, p2, p3).Wait();
                Console.WriteLine("Pinging complete");
            }



            {
                // Arrays and even enumerables can be used as parameters
                var mean = calculator.Mean(new[] { 5.0, 4.0, 3.0, 1.0, 14.0 }).Result;
                var mean2 = calculator.MeanEnum((new List<double> { 5.0, 4.0, 3.0, 1.0, 14.0 }).AsEnumerable()).Result;

                Console.WriteLine();
                Console.WriteLine("Mean of [1, 3, 4, 5, 14] equals " + mean);
                Console.WriteLine("Double check: " + mean2);
            }



            {
                // Because server actors behave just like local ones, you can 
                // choose not to use actor context on called methods
                // and they will run synchronously, so custom synchronization
                // (or no synchronization) can be applied.

                // Note, that this might be an unexpected behaviour
                // for users, so this functionality should be used
                // with care.

                // Long processing methods should be run on 
                // task pool, if actor context won't be used, otherwise
                // they will block network communication for all
                // connected clients.
                Console.WriteLine();
                Console.WriteLine("Pinging without actor context...");

                var p1 = calculator.PingAsync();
                var p2 = calculator.PingAsync();
                var p3 = calculator.PingAsync();

                Task.WhenAll(p1, p2, p3).Wait();
                Console.WriteLine("Pinging complete");
            }



            {
                // Many client actors may connect to one server.
                // Their method calls will be scheduled as they would originate
                // from single client, which means, serial execution will be enforced
                // automatically.
                Console.WriteLine();
                Console.WriteLine("Pinging with 2 clients");
                var calculator2 = ActorClientProxy.CreateActor<ICalculatorActor>("tcp://localhost:4632").Result;
               
                var p1 = calculator.Ping();
                var p2 = calculator2.Ping();

                Task.WhenAll(p1, p2).Wait();

                Console.WriteLine("Pinging done");

                // Because 'simpler' method for creating an actor was used, 
                // reference to an actor interface was received, instead of to wrapper object.
                // If wrapper object with control method is needed, returned object
                // can be cast to IActorClientProxy or IActorClientProxy<Actor Interface>
                ((IActorClientProxy)calculator2).Close();
            }

            {
                // Created actor proxy is, from the user's perspective just an interface,
                // which can also be implemented by local actor, therefore,
                // where the code executes is fully transparent for other methods.
                ICalculatorActor localCalc = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>();

                Console.WriteLine();
                Console.WriteLine("Remote: 4 + 8 = " + Add(calculator, 4, 8));
                Console.WriteLine("Local: 4 + 8 = " + Add(localCalc, 4, 8));
            }

            // Properties with IObservable<T> types can be used to 
            // broadcast messages to clients without receiving any
            // requests first.
            Console.WriteLine();
            Console.WriteLine("Random generator: ");
            var rng = calculator.Rng.Subscribe(r => Console.WriteLine(r));

            Thread.Sleep(4000);
            rng.Dispose();

            Console.WriteLine();
            calculator.Messages.Subscribe(m =>
                {
                    Console.WriteLine("A: " + m.X + "; B: " + m.Y + ". Area: " + m.Info.Field);
                });
            Thread.Sleep(3000);

            

            // When more control over proxy is needed, CreateProxy method can be used.
            // This will return the same object as CreateActor method would, but returned
            // type will be of a wrapper type.
            var proxyCalc = ActorClientProxy.CreateProxy<ICalculatorActor>("tcp://localhost:4632").Result;

            // Actual actor implementation can be accessed through .Actor property,
            // or by simply casting whole proxy to actor interface.
            // Both method will return the same object.
            ICalculatorActor proxyCalcActor1 = proxyCalc.Actor;
            ICalculatorActor proxyCalcActor2 = (ICalculatorActor)proxyCalc;

            Console.WriteLine();
            Console.WriteLine("Two methods of accessing proxy actor return the same object - " +
                                object.ReferenceEquals(proxyCalcActor1, proxyCalcActor2));


            actorServer.Stop();
            ((IActorClientProxy)calculator).Close();

            Console.WriteLine();
            Console.WriteLine("Sample finished");
            Console.Read();
        }

        private static double Add(ICalculatorActor calculator, double x, double y)
        {
            return calculator.Add(x, y).Result;
        }
    }
}
