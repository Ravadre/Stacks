using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.PlatformServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Stacks
{
#if !MONO
    public class ActionBlockExecutor : SynchronizationContext, IExecutor
    {
        private ActionBlock<Action> queue;
        private string name;

        private bool supportSynchronizationContext;

        public Task Completion { get { return queue.Completion; } }

        public event Action<Exception> Error;

        public ActionBlockExecutor()
            : this(null, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name)
            : this(name, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name, ActionBlockExecutorSettings settings)
        {
            this.name = name;
            this.supportSynchronizationContext = settings.SupportSynchronizationContext;
            this.queue = new ActionBlock<Action>(a =>
            {
                ExecuteAction(a);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = settings.QueueBoundedCapacity,
                MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism
            });
        }

        private void ExecuteAction(Action a)
        {
            SynchronizationContext oldCtx = null;
            if (this.supportSynchronizationContext)
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
                if (this.supportSynchronizationContext)
                    SynchronizationContext.SetSynchronizationContext(oldCtx);
            }
        }

        private void ErrorOccured(Exception e)
        {
            OnError(e);
            this.queue.Complete();
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
            if (!queue.Post(action))
                queue.SendAsync(action).Wait();
        }

        public Task Stop()
        {
            queue.Complete();
            return queue.Completion;
        }

        public SynchronizationContext Context
        {
            get
            {
                if (!this.supportSynchronizationContext)
                    throw new InvalidOperationException("This instance of action block executor " +
                                                        "does not support synchronization context");
                return this;
            }
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            Enqueue(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            throw new NotSupportedException();
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
