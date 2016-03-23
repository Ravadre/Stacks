using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActorClientProxy<T> : IActorClientProxy
    {
        T Actor { get; }
    }

    public interface IActorClientProxy
    {
        void Close();
        IObservable<Exception> Disconnected { get; }
    }
}
