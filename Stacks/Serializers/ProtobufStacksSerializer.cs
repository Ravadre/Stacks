using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

namespace Stacks.Serializers
{
    public class ProtobufStacksSerializer : BaseStacksSerializer
    { 
        public ProtobufStacksSerializer(IMessageHandler messageHandler)
            : base(messageHandler)
        {
            
        }

        protected override void Initialize()
        {
        }

        protected override Func<MemoryStream, T> CreateDeserializer<T>()
        {
            return ms => ProtoBuf.Serializer.Deserialize<T>(ms);
        }

        public override void Serialize<T>(T obj, MemoryStream ms)
        {
            ProtoBuf.Serializer.Serialize(ms, obj);
        }

        public override void PrepareSerializerForType<T>()
        {
            ProtoBuf.Serializer.PrepareSerializer<T>();
        }
    }
}
