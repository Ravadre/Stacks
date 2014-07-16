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

    [ProtoContract]
    public class AddMessageReply : IReplyMessage<double>
    {
        [ProtoMember(1)]
        public double Return;
        public double GetResult()
        {
            return this.Return;
        }
    }

    public class CalculatorActor: ICalculatorActor
    {
        public Task<double> Add(double x, double y)
        {
            throw new NotImplementedException();
        }

        public Task<double> Subtract(double x, double y)
        {
            throw new NotImplementedException();
        }

        public Task<double> Increment(double x)
        {
            throw new NotImplementedException();
        }

        public Task<RectangleInfo> GetRectData(Rectangle rect)
        {
            throw new NotImplementedException();
        }

        public Task<TriangleInfo> GetTriangleData(Triangle triangle)
        {
            throw new NotImplementedException();
        }

        public Task<TriangleInfo> GetTriangleData2(Triangle triangle, double f)
        {
            throw new NotImplementedException();
        }

        public Task PushInfo(double x)
        {
            throw new NotImplementedException();
        }

        public Task Ping()
        {
            throw new NotImplementedException();
        }
    }
}
