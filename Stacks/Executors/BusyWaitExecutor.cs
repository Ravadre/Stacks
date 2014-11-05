using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;

namespace Stacks
{
    public class BusyWaitExecutor : SynchronizationContext, IExecutor
    {
        private string name;
        private BlockingCollection<Action> col;
        private TaskCompletionSource<int> tcs;
        private volatile bool isStopping;
        private volatile bool stopImmediately;
        private Thread runner;

        public event Action<Exception> Error;

        public string Name { get { return name; } }

        public BusyWaitExecutor()
            : this(null)
        { }

        public BusyWaitExecutor(string name)
        {
            this.name = name == null ? string.Empty : name;
            col = new BlockingCollection<Action>();
            tcs = new TaskCompletionSource<int>();
            runner = new Thread(new ThreadStart(Run));
            runner.IsBackground = true;
            runner.Start();
        }

        private void Run()
        {
            Action a;

            while (true)
            {
                if (this.isStopping && (col.Count == 0 || stopImmediately))
                    break;

                if (col.TryTake(out a, 50))
                {
                    ExecuteAction(a);
                }
            }

            tcs.SetResult(0);
        }

        private void ExecuteAction(Action a)
        {
            var oldCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(this);

            try
            {
                a();
            }
            catch (Exception e)
            {
                ErrorOccured(e);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldCtx);
            }
        }

        private void ErrorOccured(Exception e)
        {
            OnError(e);
            isStopping = true;
        }

        private void OnError(Exception e)
        {
            var h = Error;
            if (h != null)
            {
                try { h(e); }
                catch { }
            }
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            base.Send(d, state);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            Enqueue(() => d(state));
        }

        public void Enqueue(Action action)
        {
            if (!isStopping)
                col.Add(action);
        }

        public Task<System.Reactive.Unit> PostTask(Action action)
        {
            return ExecutorHelper.PostTask(this, action);
        }

        public Task<T> PostTask<T>(Func<T> func)
        {
            return ExecutorHelper.PostTask(this, func);
        }

        public Task Stop()
        {
            return Stop(stopImmediately: false);
        }

        public Task Stop(bool stopImmediately)
        {
            isStopping = true;
            this.stopImmediately = stopImmediately;
            return tcs.Task as Task;
        }

        public Task Completion
        {
            get { return tcs.Task as Task; }
        }

        public SynchronizationContext Context
        {
            get { return this; }
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

        public override string ToString()
        {
            return "BusyWait Executor " +
                (string.IsNullOrWhiteSpace(name) ? "" : string.Format("({0})", name));
        }
    }
}
