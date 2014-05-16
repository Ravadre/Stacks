using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks;

namespace ProtobufSample
{
    [ProtoContract]
    [StacksMessage(3)]
    public class TemperatureChanged
    {
        [ProtoMember(1)]
        public string City { get; set; }
        [ProtoMember(2)]
        public double Temperature { get; set; }
    }

    [ProtoContract]
    [StacksMessage(1)]
    public class TemperatureRequest
    {
        [ProtoMember(1)]
        public string City { get; set; }
    }

    [ProtoContract]
    [StacksMessage(2)]
    public class TemperatureResponse
    {
        [ProtoMember(1)]
        public string City { get; set; }
        [ProtoMember(2)]
        public double Temperature { get; set; }
    }
}
