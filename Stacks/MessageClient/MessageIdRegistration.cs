using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public class MessageIdRegistration
    {
        private readonly ImperativeMessageIdCache cache;

        internal MessageIdRegistration()
        {
            cache = new ImperativeMessageIdCache();
        }

        public MessageIdRegistration RegisterMessage<T>(int messageId)
        {
            cache.RegisterMessageId(messageId, typeof(T));

            return this;
        }

        public MessageIdRegistration RegisterMessage(int messageId, Type type)
        {
            cache.RegisterMessageId(messageId, type);

            return this;
        }

        internal IMessageIdCache CreateCache()
        {
            return cache;
        }
    }
}
