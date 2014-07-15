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
    [ProtoContract]
    public class TriangleInfo
    {
        [ProtoMember(1)]
        public double Field { get; set; }
        [ProtoMember(2)]
        public double Height { get; set; }
    }

    public class Rectangle
    {
        public double A { get; set; }
        public double B { get; set; }
    }

    public class RectangleInfo
    {
        public double Field { get; set; }
        public double Perimeter { get; set; }
    }

    [ProtoContract]
    public class Triangle
    {
        [ProtoMember(1)]
        public double A { get; set; }
        [ProtoMember(2)]
        public double B { get; set; }
        [ProtoMember(3)]
        public double C { get; set; }
    }

    public interface ICalculatorActor
    {
        Task<double> Add(double x, double y);
        Task<double> Subtract(double x, double y);
        Task<double> Increment(double x);

        Task<RectangleInfo> GetRectData(Rectangle rect);
        Task<TriangleInfo> GetTriangleData(Triangle triangle);
        Task<TriangleInfo> GetTriangleData2(Triangle triangle, double f);

        Task PushInfo(double x);
        Task Ping();
    }

    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server();

            ICalculatorActor calculator = ActorClientProxy.Create<ICalculatorActor>("tcp://127.0.0.1:4632");
            Console.WriteLine("Result is: " + calculator.Add(5, 4).Result);

            Console.ReadKey();

        }
    }
}
