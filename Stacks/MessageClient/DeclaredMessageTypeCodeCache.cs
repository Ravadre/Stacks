using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class DeclaredMessageTypeCodeCache : IMessageTypeCodeCache
    {
        private Dictionary<Type, int> typeCodeByType;

        ReaderWriterLockSlim rwLock;

        public DeclaredMessageTypeCodeCache()
        {
            typeCodeByType = new Dictionary<Type, int>();

            rwLock = new ReaderWriterLockSlim();
        }

        public void PreLoadTypesFromAssemblyOfType<T>()
        {
            throw new InvalidOperationException(
                "Cannot load types from assembly if type codes were declared imperatively");
        }

        internal void RegisterTypeCode(int typeCode, Type type)
        {
            try
            {
                rwLock.EnterWriteLock();

                typeCodeByType[type] = typeCode;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public int GetTypeCode<T>()
        {
            return GetTypeCode(typeof(T));
        }

        public int GetTypeCode(Type t)
        {
            try
            {
                rwLock.EnterReadLock();

                int typeCode;
                if (typeCodeByType.TryGetValue(t, out typeCode))
                {
                    return typeCode;
                }
                else
                {
                    throw new InvalidDataException(string.Format("Cannot resolve type id for type {0}. " +
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

                if (typeCodeByType.ContainsKey(t))
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
