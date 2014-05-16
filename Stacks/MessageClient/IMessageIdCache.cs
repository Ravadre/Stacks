using System;

namespace Stacks
{
    public interface IMessageIdCache
    {
        int GetMessageId(Type t);
        int GetMessageId<T>();
        void PreLoadType(Type t);
        void PreLoadType<T>();
        void PreLoadTypesFromAssemblyOfType<T>();
    }
}
