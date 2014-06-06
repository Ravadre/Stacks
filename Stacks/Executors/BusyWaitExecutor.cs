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
        private BlockingCollection<Action> col;
        private TaskCompletionSource<int> tcs;
        private volatile bool isStopping;
        private Thread runner;

        public event Action<Exception> Error;

        public BusyWaitExecutor()
        {
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
                if (this.isStopping && col.Count == 0)
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

        public void Enqueue(Action action)
        {
            if (!isStopping)
                col.Add(action);
        }

        public Task Stop()
        {
            isStopping = true;
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
