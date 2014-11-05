using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public interface IExecutor : IScheduler
    {
        string Name { get; }

        event Action<Exception> Error;

        void Enqueue(Action action);
        Task<Unit> PostTask(Action action);
        Task<T> PostTask<T>(Func<T> func);

        Task Stop();
        Task Stop(bool stopImmediately);
        Task Completion { get; }

        SynchronizationContext Context { get; }
    }
}
