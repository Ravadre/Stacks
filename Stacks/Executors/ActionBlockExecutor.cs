using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
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
        private readonly ActionBlock<Action> queue;
        private readonly bool supportSynchronizationContext;
        private volatile bool stopImmediately;

        public Task Completion => queue.Completion;
        public event Action<Exception> Error;
        public string Name { get; set; }


        public ActionBlockExecutor(string name)
            : this(name, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor()
            : this(null, new ActionBlockExecutorSettings())
        { }

        public ActionBlockExecutor(string name, ActionBlockExecutorSettings settings)
        {
            Name = name;
            this.supportSynchronizationContext = settings.SupportSynchronizationContext;
            this.queue = new ActionBlock<Action>(a =>
            {
                if (stopImmediately) return;

                ExecuteAction(a);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = settings.QueueBoundedCapacity,
                MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism,
            });
        }

        public ActionBlockExecutor(ActionBlockExecutorSettings settings)
            : this(null, settings)
        { }

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
            try
            {
                Error?.Invoke(e);
            }
            catch
            { 
                // Ignore 
            }
        }

        public Task<System.Reactive.Unit> PostTask(Action action)
        {
            return ExecutorHelper.PostTask(this, action);
        }

        public Task<T> PostTask<T>(Func<T> func)
        {
            return ExecutorHelper.PostTask(this, func);
        }

        public void Enqueue(Action action)
        {
            if (!queue.Post(action))
                queue.SendAsync(action).Wait();
        }

        public Task Stop(bool stopImmediately)
        {
            this.stopImmediately = stopImmediately;
            queue.Complete();
            return queue.Completion;
        }

        public Task Stop()
        {
            return Stop(stopImmediately: false);
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
                (string.IsNullOrWhiteSpace(Name) ? "" : $"({Name})");
        }


        DateTimeOffset IScheduler.Now => DateTimeOffset.UtcNow;

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
