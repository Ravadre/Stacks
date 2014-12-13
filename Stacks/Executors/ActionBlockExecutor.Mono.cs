using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace Stacks
{
#if MONO
    public class ActionBlockExecutor : SynchronizationContext, IExecutor
    {
        private BlockingCollection<Action> col;
        private TaskCompletionSource<int> tcs;
        private volatile bool isStopping;
        private volatile bool stopImmediately;
        private Thread runner;

        private readonly string name;
        private readonly bool supportSynchronizationContext;

        public event Action<Exception> Error;
          
        public string Name { get { return name; } }

        public ActionBlockExecutor()
            : this(null, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name)
            : this(name, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name, ActionBlockExecutorSettings settings)
        {
            this.name = name == null ? string.Empty : name;
            this.supportSynchronizationContext = settings.SupportSynchronizationContext;
         
            if (settings.QueueBoundedCapacity <= 0)
                col = new BlockingCollection<Action>();
            else
                col = new BlockingCollection<Action>(settings.QueueBoundedCapacity);
           
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
            SynchronizationContext oldCtx = null;

            if (supportSynchronizationContext)
            {
                oldCtx = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(this);
            }

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
                if (supportSynchronizationContext)
                {
                    SynchronizationContext.SetSynchronizationContext(oldCtx);
                }
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
            this.stopImmediately = stopImmediately;
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

        public override string ToString()
        {
            return "ActionBlock Executor " +
                (name == null ? "" : string.Format("({0})", name));
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

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            Enqueue(() =>
            {
                action(this, state);
            });

            return Disposable.Empty;
        }
    }
#endif
}
