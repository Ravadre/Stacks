using System;

namespace Stacks
{
    public interface IMessageTypeCodeCache
    {
        int GetTypeCode(Type t);
        int GetTypeCode<T>();
        void PreLoadType(Type t);
        void PreLoadType<T>();
        void PreLoadTypesFromAssemblyOfType<T>();
    }
}
