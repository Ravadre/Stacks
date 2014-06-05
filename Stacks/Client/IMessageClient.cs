using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IMessageClient : ISocketClient
    {
        IObservable<int> Sent { get; }

        void Send<T>(T obj);

        void PreLoadTypesFromAssemblyOfType<T>();
        void PreLoadType<T>();
    }
}
