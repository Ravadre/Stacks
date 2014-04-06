using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Client
{
    public interface IMessageClient
    {
        event Action<int> Sent;
        event Action<Exception> Disconnected;

        void Send<T>(int typeCode, T obj);

        void Close();
    }
}
