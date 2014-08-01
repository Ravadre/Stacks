using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks.Actors;

namespace RemoteActorsSample
{
    [ProtoContract]
    public class Rectangle
    {
        [ProtoMember(1)]
        public double A { get; set; }
        [ProtoMember(2)]
        public double B { get; set; }
    }

    [ProtoContract]
    public class RectangleInfo
    {
        [ProtoMember(1)]
        public double Field { get; set; }
        [ProtoMember(2)]
        public double Perimeter { get; set; }
    }

    [ProtoContract]
    public class Message
    {
        [ProtoMember(1)]
        public long X { get; set; }
        [ProtoMember(2)]
        public long Y { get; set; }
        [ProtoMember(3)]
        public RectangleInfo Info { get; set; }
    }

    public interface ICalculatorActor : IActorClientProxy
    {
        Task<double> Add(double x, double y);
        Task<double> Subtract(double x, double y);
        Task<int> Increment(int x);

        Task<RectangleInfo> CalculateRectangle(Rectangle rect);

        Task PushNumber(double x);
        Task<double> PopNumber();
        Task Ping();

        Task<double> Mean(double[] xs);
        Task<double> MeanEnum(IEnumerable<double> xs);

        Task PingAsync();

        IObservable<double> Rng { get; }
        IObservable<Message> Messages { get; }
    }
}
