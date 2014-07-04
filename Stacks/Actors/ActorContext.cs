using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class ActorContext : IActorContext, INotifyCompletion
    {
        private IExecutor executor;
        private string name;

        public ActorContext()
            : this(null, 
                   new ActionBlockExecutor(null, ActionBlockExecutorSettings.Default))
        { }

        public ActorContext(string name)
            : this(name,
                   new ActionBlockExecutor(name, ActionBlockExecutorSettings.Default))
        { }

        public ActorContext(string name, IExecutor executor)
        {
            this.name = name;
            this.executor = executor;
        }

        public Task Completion { get { return executor.Completion; } }

        public Task Stop()
        {
            return executor.Stop();
        }

        public void Post(Action action)
        {
            executor.Enqueue(action);
        }
        
        public Task<System.Reactive.Unit> PostTask(Action action)
        {
            return executor.PostTask(action);
        }

        public Task<T> PostTask<T>(Func<T> func)
        {
            return executor.PostTask(func);
        }


        public IActorContext GetAwaiter() { return this; }

        public bool IsCompleted
        {
            get { return false; }
        }

        public void OnCompleted(Action continuation)
        {
            executor.Enqueue(continuation);
        }

        public void GetResult() { }

        public SynchronizationContext Context { get { return executor.Context; } }

        public override string ToString()
        {
            return name == null ?
                "Dispatcher context" : string.Format("Dispatcher context ({0})", name);
        }


        public static ActorContext FromCurrentSynchronizationContext()
        {
            var context = SynchronizationContext.Current;

            if (context == null)
                throw new InvalidOperationException("No Synchronization context is set");

            return new ActorContext(null, new CapturedContextExecutor(null, context));
        }


        DateTimeOffset IScheduler.Now
        {
            get { return DateTimeOffset.UtcNow; }
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return executor.Schedule(state, dueTime, action);
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return executor.Schedule(state, dueTime, action);
        }

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            return executor.Schedule(state, action);
        }
    }
}
