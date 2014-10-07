using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActorContext : INotifyCompletion, IScheduler
    {
        bool IsCompleted { get; }

        void Post(Action action);
        Task<System.Reactive.Unit> PostTask(Action action);
        Task<T> PostTask<T>(Func<T> func);

        IActorContext GetAwaiter();
        void GetResult();

        event Action<Exception> Error;
    }
}
