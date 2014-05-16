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
    public class MessageTypeCodeCache
    {
        private Dictionary<Type, int> typeCodeByType;
        private Dictionary<int, Type> typeByTypeCode;

        ReaderWriterLockSlim rwLock;

        public MessageTypeCodeCache()
        {
            typeCodeByType = new Dictionary<Type, int>();
            typeByTypeCode = new Dictionary<int, Type>();

            rwLock = new ReaderWriterLockSlim();

        }

        public void PreLoadTypesFromAssembly<T>()
        {
            var codeByTypeLocal = new Dictionary<Type, int>();
            var typeByCodeLocal = new Dictionary<int, Type>();

            foreach (var t in typeof(T).Assembly.GetTypes()
                                       .Where(t => !t.IsInterface)
                                       .Where(t => !t.IsAbstract))
            {
                var attr = t.GetCustomAttribute<StacksMessageAttribute>();

                if (attr != null)
                {
                    codeByTypeLocal[t] = attr.TypeCode;
                    typeByCodeLocal[attr.TypeCode] = t;
                }
            }
                              
            try
            {
                rwLock.EnterWriteLock();

                foreach (var kv in codeByTypeLocal)
                {
                    typeCodeByType[kv.Key] = kv.Value;
                }

                foreach (var kv in typeByCodeLocal)
                {
                    typeByTypeCode[kv.Key] = kv.Value;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public int GetTypeCode<T>()
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                int typeCode;
                if (typeCodeByType.TryGetValue(typeof(T), out typeCode))
                {
                    return typeCode;
                }
                else
                {
                    var attribute = typeof(T).GetCustomAttribute<StacksMessageAttribute>();

                    if (attribute == null)
                    {
                        throw new InvalidDataException(string.Format("Cannot resolve type id for type {0}. " +
                        "It has no {1} attribute and it wasn't declared imperatively",
                            typeof(T).Name, typeof(StacksMessageAttribute).Name));
                    }

                    try
                    {
                        rwLock.EnterWriteLock();

                        typeCodeByType[typeof(T)] = attribute.TypeCode;
                        typeByTypeCode[attribute.TypeCode] = typeof(T);

                        return attribute.TypeCode;
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
            Type t = typeof(T);
            var attr = t.GetCustomAttribute<StacksMessageAttribute>();

            if (attr != null)
            {
                try
                {
                    rwLock.EnterWriteLock();

                    typeByTypeCode[attr.TypeCode] = t;
                    typeCodeByType[t] = attr.TypeCode;
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
            
        }
    }
}
