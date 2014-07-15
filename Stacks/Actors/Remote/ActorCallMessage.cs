using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace Stacks.Actors
{
    [ProtoContract]
    class ActorCallMessage
    {
        [ProtoMember(1)]
        public string MessageName { get; set; }
        [ProtoMember(2)]
        public bool IsRequestCall { get; set; }
    }
}
