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

    public class CalculatorActor: Actor, ICalculatorActor
    {
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

        public async Task<double> Increment(double x)
        {
            await Context;

            return x + 1.0;
        }

        public async Task<RectangleInfo> GetRectData(Rectangle rect)
        {
            await Context;

            return new RectangleInfo()
            {
                Field = rect.A * rect.B,
                Perimeter = rect.A * 2.0 + rect.B * 2.0
            };
        }

        public async Task<TriangleInfo> GetTriangleData(Triangle triangle)
        {
            await Context;

            return new TriangleInfo()
            {
                 Field = 1.0,
                 Height = 1.0
            };
        }

        public async Task<TriangleInfo> GetTriangleData2(Triangle triangle, double f)
        {
            await Context;

            return new TriangleInfo()
            {
                Field = 1.0,
                Height = 1.0
            };
        }

        public async Task PushInfo(double x)
        {
            await Context;

            throw new Exception("Can't push info with parameter x = " + x);
        }

        public async Task Ping()
        {
            await Context;

            Console.WriteLine("Ping");
            Thread.Sleep(2000);
        }

        public void Close() { }
    }
}
