using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class CapturedContextExecutor : SynchronizationContext, IExecutor
    {
        private string name;
        private SynchronizationContext context;
        private TaskCompletionSource<int> tcs;

        public Task Completion { get { return tcs.Task; } }

#pragma warning disable 67
        public event Action<Exception> Error;
#pragma warning restore 67

        public CapturedContextExecutor(string name, SynchronizationContext context)
        {
            Error = null;
            this.name = name;
            this.context = context;
            tcs = new TaskCompletionSource<int>();
        }

        public void Enqueue(Action action)
        {
            context.Post(_ => action(), null);
        }

        public Task Stop()
        {
            tcs.SetResult(0);
            return tcs.Task;
        }

        public SynchronizationContext Context
        {
            get { return context; }
        }

        public override SynchronizationContext CreateCopy()
        {
            return context.CreateCopy();
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            context.Post(d, state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return "CapturedContext Executor" +
                (name == null ? "" : string.Format("({0})", name));
        }


        DateTimeOffset IScheduler.Now
        {
            get { return DateTimeOffset.UtcNow; }
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<System.Reactive.Concurrency.IScheduler, TState, IDisposable> action)
        {
            throw new NotImplementedException();
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<System.Reactive.Concurrency.IScheduler, TState, IDisposable> action)
        {
            throw new NotImplementedException();
        }

        public IDisposable Schedule<TState>(TState state, Func<System.Reactive.Concurrency.IScheduler, TState, IDisposable> action)
        {
            Enqueue(() =>
            {
                action(this, state);
            });

            return Disposable.Empty;
        }
    }
}
