using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IExecutor
    {
        event Action<Exception> Error;

        void Enqueue(Action action);

        Task Stop();
        Task Completion { get; }

        SynchronizationContext Context { get; }
    }
}
