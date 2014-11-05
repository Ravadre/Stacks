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

        public string Name { get { return name; } }

        public CapturedContextExecutor(string name, SynchronizationContext context)
        {
            Error = null;
            this.name = name == null ? string.Empty : name;
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

        public Task Stop(bool stopImmediately)
        {
            return Stop();
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

        public Task<System.Reactive.Unit> PostTask(Action action)
        {
            return ExecutorHelper.PostTask(this, action);
        }

        public Task<T> PostTask<T>(Func<T> func)
        {
            return ExecutorHelper.PostTask(this, func);
        }

        public override string ToString()
        {
            return "CapturedContext Executor " +
                (string.IsNullOrWhiteSpace(name) ? "" : string.Format("({0})", name));
        }


        DateTimeOffset IScheduler.Now
        {
            get { return DateTimeOffset.UtcNow; }
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            var due = dueTime - DateTimeOffset.UtcNow;

            return Schedule(state, due, action);
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            Task.Delay(dueTime)
                .ContinueWith(t =>
                {
                    Schedule(state, action);
                });

            return Disposable.Empty;
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
