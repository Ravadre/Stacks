using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class MessageIdCache : IMessageIdCache
    {
        private Dictionary<Type, int> messageIdByType;

        ReaderWriterLockSlim rwLock;

        public MessageIdCache()
        {
            messageIdByType = new Dictionary<Type, int>();

            rwLock = new ReaderWriterLockSlim();

        }

        public void PreLoadTypesFromAssemblyOfType<T>()
        {
            var idByTypeLocal = new Dictionary<Type, int>();

            foreach (var t in typeof(T).Assembly.GetTypes()
                                       .Where(t => !t.IsInterface)
                                       .Where(t => !t.IsAbstract))
            {
                var attr = t.GetCustomAttribute<StacksMessageAttribute>();

                if (attr != null)
                {
                    idByTypeLocal[t] = attr.MessageId;
                }
            }
                              
            try
            {
                rwLock.EnterWriteLock();

                foreach (var kv in idByTypeLocal)
                {
                    messageIdByType[kv.Key] = kv.Value;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public int GetMessageId<T>()
        {
            return GetMessageId(typeof(T));
        }

        public int GetMessageId(Type t)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                int messageid;
                if (messageIdByType.TryGetValue(t, out messageid))
                {
                    return messageid;
                }
                else
                {
                    var attribute = t.GetCustomAttribute<StacksMessageAttribute>();

                    if (attribute == null)
                    {
                        throw new InvalidDataException(string.Format("Cannot resolve type id for type {0}. " +
                        "It has no {1} attribute and it wasn't declared imperatively",
                            t.Name, typeof(StacksMessageAttribute).Name));
                    }

                    try
                    {
                        rwLock.EnterWriteLock();

                        messageIdByType[t] = attribute.MessageId;

                        return attribute.MessageId;
                    }
                    finally { rwLock.ExitWriteLock(); }
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        public void PreLoadType<T>()
        {
            PreLoadType(typeof(T));
        }

        public void PreLoadType(Type t)
        {
            var attr = t.GetCustomAttribute<StacksMessageAttribute>();

            if (attr != null)
            {
                try
                {
                    rwLock.EnterWriteLock();

                    messageIdByType[t] = attr.MessageId;
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
        }
    }
}
