using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    internal class ActorContext : IActorContext
    {
        private readonly IExecutor executor;
        private string name;

        public ActorContext()
            : this(new ActionBlockExecutor(ActionBlockExecutorSettings.Default))
        {
        }

        public ActorContext(ActorContextSettings settings)
            : this(
                new ActionBlockExecutor(ActionBlockExecutorSettings.DefaultWith(settings.SupportSynchronizationContext))
                )
        {
        }

        public ActorContext(IExecutor executor)
        {
            this.executor = executor;
        }

        public Task Completion => executor.Completion;

        public event Action<Exception> Error
        {
            add { executor.Error += value; }
            remove { executor.Error -= value; }
        }

        public Task Stop(bool stopImmediately)
        {
            return executor.Stop(stopImmediately);
        }

        public Task Stop()
        {
            return Stop(false);
        }

        public void Post(Action action)
        {
            executor.Enqueue(action);
        }

        public Task<Unit> PostTask(Action action)
        {
            return executor.PostTask(action);
        }

        public Task<T> PostTask<T>(Func<T> func)
        {
            return executor.PostTask(func);
        }

        public IActorContext GetAwaiter()
        {
            return this;
        }

        public bool IsCompleted => false;

        public void OnCompleted(Action continuation)
        {
            executor.Enqueue(continuation);
        }

        public void GetResult()
        {
        }

        public SynchronizationContext SynchronizationContext => executor.Context;
        DateTimeOffset IScheduler.Now => DateTimeOffset.UtcNow;

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime,
            Func<IScheduler, TState, IDisposable> action) 
            => executor.Schedule(state, dueTime, action);

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime,
            Func<IScheduler, TState, IDisposable> action) 
            => executor.Schedule(state, dueTime, action);

        public IDisposable Schedule<TState>(TState state,
            Func<IScheduler, TState, IDisposable> action)
            => executor.Schedule(state, action);

        internal void SetName(string newName)
        {
            name = newName;
        }

        public override string ToString()
        {
            return name == null
                ? "Dispatcher context"
                : $"Dispatcher context ({name})";
        }

        public static ActorContext FromCurrentSynchronizationContext()
        {
            var context = SynchronizationContext.Current;

            if (context == null)
                throw new InvalidOperationException("No Synchronization context is set");

            return new ActorContext(new CapturedContextExecutor(context));
        }
    }
}