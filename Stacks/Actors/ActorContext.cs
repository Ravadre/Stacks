using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    internal class ActorContext : IActorContext
    {
        private readonly Actor actor;
        private readonly IExecutor executor;
        private string name;

        public ActorContext(Actor actor)
            : this(actor, new ActionBlockExecutor(ActionBlockExecutorSettings.Default))
        {
        }

        public ActorContext(Actor actor, ActorContextSettings settings)
            : this(actor,
                new ActionBlockExecutor(ActionBlockExecutorSettings.DefaultWith(settings.SupportSynchronizationContext))
                )
        {
        }

        public ActorContext(Actor actor, IExecutor executor)
        {
            this.actor = actor;
            this.executor = executor;
        }

        public Task Completion => executor.Completion;
        
        public Task Stop(bool stopImmediately)
        {
            return executor.Stop(stopImmediately);
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

        public bool IsCompleted => executor.Completion.IsCompleted;

        public void OnCompleted(Action continuation)
        {
            executor.Enqueue(continuation);
        }

        public void GetResult()
        {
            if (executor.Completion.IsCompleted)
                throw new ActorStoppedException("Actor context is stopped. New tasks cannot be queued. Further awaits will result in exception being thrown.");
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
    }
}