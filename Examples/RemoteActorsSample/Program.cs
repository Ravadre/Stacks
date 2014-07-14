using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Actors;

namespace RemoteActorsSample
{
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

    interface ICalculatorActor
    {
        Task<double> Add(double x, double y);
        Task<double> Subtract(double x, double y);
        Task<double> Increment(double x);

        Task<RectangleInfo> GetRectData(Rectangle rect);
    }

    class Program
    {
        static void Main(string[] args)
        {
            var calculator = ActorClientProxy.Create<ICalculatorActor>(new IPEndPoint(IPAddress.Loopback, 4632));


        }
    }
}
