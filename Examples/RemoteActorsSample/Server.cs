using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Stacks;
using Stacks.Tcp;
using Stacks.Actors;
using System.Net;

using System.Reactive.Linq;
using System.IO;
using ProtoBuf;
using System.Threading;
using System.Reactive.Subjects;

namespace RemoteActorsSample
{
    [ProtoContract]
    public class AddMessage
    {
        [ProtoMember(1)]
        public double x;
        [ProtoMember(2)]
        public double y;
    }

    public class CalculatorActor : Actor, ICalculatorActor
    {
        private Stack<double> stack = new Stack<double>();

        private Random rng = new Random();
        public IObservable<double> Rng { get; private set; }

        private long x;
        private Subject<Message> messages;
        public IObservable<Message> Messages { get { return messages; } }

        public CalculatorActor()
        {
            Rng = Observable.Interval(TimeSpan.FromSeconds(1.0), Context)
                            .Select(t => rng.NextDouble());
            messages = new Subject<Message>();

            Task.Run(async () =>
                {
                    while (true)
                    {
                        ++x;
                        await Task.Delay(1000);
                        messages.OnNext(new Message
                            {
                                X = x,
                                Y = x * 2,
                                Info = new RectangleInfo
                                {
                                    Field = x * (x * 2),
                                    Perimeter = x * 6
                                }
                            });
                    }
                });
        }

        public async Task<double> Add(double x, double y)
        {
            await Context;

            return x + y;
        }

        public async Task<double> Subtract(double x, double y)
        {
            await Context;

            return x - y;
        }

        public async Task<int> Increment(int x)
        {
            await Context;

            return ++x;
        }

        public async Task<RectangleInfo> CalculateRectangle(Rectangle rect)
        {
            await Context;

            return new RectangleInfo()
            {
                Field = rect.A * rect.B,
                Perimeter = rect.A * 2.0 + rect.B * 2.0
            };
        }

        public async Task Ping()
        {
            await Context;

            Console.WriteLine("Ping");
            Thread.Sleep(1000);
        }

        public async Task PushNumber(double x)
        {
            await Context;

            stack.Push(x);
        }

        public async Task<double> PopNumber()
        {
            await Context;

            return stack.Pop();
        }

        public async Task<double> Mean(double[] xs)
        {
            await Context;

            return xs.Average();
        }

        public async Task<double> MeanEnum(IEnumerable<double> xs)
        {
            await Context;

            return xs.Average();
        }

        public Task PingAsync()
        {
            return Task.Run(() =>
                {
                    Console.WriteLine("Ping async");
                    Thread.Sleep(1000);
                });
        }
    }
}
