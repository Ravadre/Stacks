using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public class MessageTypeCodeRegistration
    {
        private DeclaredMessageTypeCodeCache cache;

        internal MessageTypeCodeRegistration()
        {
            cache = new DeclaredMessageTypeCodeCache();
        }

        public MessageTypeCodeRegistration RegisterMessage<T>(int typeCode)
        {
            cache.RegisterTypeCode(typeCode, typeof(T));

            return this;
        }

        public MessageTypeCodeRegistration RegisterMessage(int typeCode, Type type)
        {
            cache.RegisterTypeCode(typeCode, type);

            return this;
        }

        internal IMessageTypeCodeCache CreateCache()
        {
            return cache;
        }
    }
}
