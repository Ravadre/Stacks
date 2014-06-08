using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IExecutor : IScheduler
    {
        event Action<Exception> Error;

        void Enqueue(Action action);

        Task Stop();
        Task Completion { get; }

        SynchronizationContext Context { get; }
    }
}
