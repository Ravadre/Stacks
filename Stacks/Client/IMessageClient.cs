using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IMessageClient : ISocketClient
    {
        event Action<int> Sent;
        IObservable<Exception> Disconnected { get; }

        void Send<T>(T obj);

        void PreLoadTypesFromAssemblyOfType<T>();
        void PreLoadType<T>();

        void Close();
    }
}
