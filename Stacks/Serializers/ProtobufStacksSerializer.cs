using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

namespace Stacks
{
    public class ProtoBufStacksSerializer : IStacksSerializer
    {
        public ProtoBufStacksSerializer()
        {

        }

        public void Initialize()
        {
        }

        public T Deserialize<T>(MemoryStream ms)
        {
            return ProtoBuf.Serializer.Deserialize<T>(ms);
        }

        public void Serialize<T>(T obj, MemoryStream ms)
        {
            ProtoBuf.Serializer.Serialize(ms, obj);
        }

        public void PrepareSerializerForType<T>()
        {
            ProtoBuf.Serializer.PrepareSerializer<T>();
        }
    }
}
