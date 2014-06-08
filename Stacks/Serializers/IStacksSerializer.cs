using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IStacksSerializer
    {
        void Initialize();

        //Func<MemoryStream, T> CreateDeserializer<T>();
        T Deserialize<T>(MemoryStream ms);
        void Serialize<T>(T obj, MemoryStream ms);

        void PrepareSerializerForType<T>();
    }
}
