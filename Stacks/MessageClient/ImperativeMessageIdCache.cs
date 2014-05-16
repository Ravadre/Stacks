using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class ImperativeMessageIdCache : IMessageIdCache
    {
        private Dictionary<Type, int> messageIdByType;

        ReaderWriterLockSlim rwLock;

        public ImperativeMessageIdCache()
        {
            messageIdByType = new Dictionary<Type, int>();

            rwLock = new ReaderWriterLockSlim();
        }

        public void PreLoadTypesFromAssemblyOfType<T>()
        {
            throw new InvalidOperationException(
                "Cannot load types from assembly if message ids were declared imperatively");
        }

        internal void RegisterMessageId(int messageId, Type type)
        {
            try
            {
                rwLock.EnterWriteLock();

                messageIdByType[type] = messageId;
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
                rwLock.EnterReadLock();

                int messageId;
                if (messageIdByType.TryGetValue(t, out messageId))
                {
                    return messageId;
                }
                else
                {
                    throw new InvalidDataException(string.Format("Cannot resolve message id for type {0}. " +
                        "It has no {1} attribute and it wasn't declared imperatively",
                            t.Name, typeof(StacksMessageAttribute).Name));
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public void PreLoadType<T>()
        {
            PreLoadType(typeof(T));
        }

        public void PreLoadType(Type t)
        {
            try
            {
                rwLock.EnterReadLock();

                if (messageIdByType.ContainsKey(t))
                    return;
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            throw new InvalidOperationException(
                "Message client tried to preload new type " + t.Name + ". " +
                "In most cases this means that message handler tries to handle packet type " +
                "which was not registered");
        }
    }
}
