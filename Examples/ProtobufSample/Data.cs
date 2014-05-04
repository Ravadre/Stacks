using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace ProtobufSample
{
    [ProtoContract]
    public class TemperatureChanged
    {
        [ProtoMember(1)]
        public string City { get; set; }
        [ProtoMember(2)]
        public double Temperature { get; set; }
    }

    [ProtoContract]
    public class TemperatureRequest
    {
        [ProtoMember(1)]
        public string City { get; set; }
    }

    [ProtoContract]
    public class TemperatureResponse
    {
        [ProtoMember(1)]
        public string City { get; set; }
        [ProtoMember(2)]
        public double Temperature { get; set; }
    }
}
