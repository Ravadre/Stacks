using System.IO;
using ProtoBuf;

namespace Stacks
{
    public class ProtoBufStacksSerializer : IStacksSerializer
    {
        public void Initialize()
        {
        }

        public T Deserialize<T>(MemoryStream ms)
        {
            return Serializer.Deserialize<T>(ms);
        }

        public void Serialize<T>(T obj, MemoryStream ms)
        {
            Serializer.Serialize(ms, obj);
        }

        public void PrepareSerializerForType<T>()
        {
            Serializer.PrepareSerializer<T>();
        }
    }
}
