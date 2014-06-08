using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public class MessagePackStacksSerializer : IStacksSerializer
    {
        private SerializationContext context;

        public MessagePackStacksSerializer()
        {
            
        }

        public void Initialize()
        {
            this.context = new SerializationContext();
        }

        public T Deserialize<T>(MemoryStream ms)
        {
            var d = MessagePackSerializer.Create<T>(this.context);
            return d.Unpack(ms);
        }

        public void Serialize<T>(T obj, MemoryStream ms)
        {
            var s = MessagePackSerializer.Create<T>(this.context);
            s.Pack(ms, obj);
        }

        public void PrepareSerializerForType<T>()
        {
        }
    }
}
