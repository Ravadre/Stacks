using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Serializers
{
    public class MessagePackStacksSerializer : BaseStacksSerializer
    {
        private SerializationContext context;

        public MessagePackStacksSerializer(IMessageHandler messageHandler)
            : base(messageHandler)
        {
            
        }

        protected override void Initialize()
        {
            this.context = new SerializationContext();
        }

        protected override Func<MemoryStream, T> CreateDeserializer<T>()
        {
            var d = MessagePackSerializer.Create<T>(this.context);
            return ms => d.Unpack(ms);
        }

        public override void Serialize<T>(T obj, MemoryStream ms)
        {
            var s = MessagePackSerializer.Create<T>(this.context);
            s.Pack(ms, obj);
        }
    }
}
