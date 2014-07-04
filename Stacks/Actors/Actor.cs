using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class Actor : IScheduler
    {
        private readonly ActorContext context;
        private readonly string name;

        public Actor()
            : this(null, 
                   new ActionBlockExecutor(null, ActionBlockExecutorSettings.Default))
        { }

        public Actor(string name)
            : this(name, 
                   new ActionBlockExecutor(name, ActionBlockExecutorSettings.Default))
        { }

        public Actor(string name, IExecutor executor)
        {
            this.name = name;
            this.context = new ActorContext(name, executor);
        }

        protected Task Completion { get { return context.Completion; } }

        protected Task Stop()
        {
            return context.Stop();
        }

        public bool Named { get { return name != null; } }
        public string Name { get { return name; } }

        protected IActorContext Context { get { return context; } }
        protected SynchronizationContext GetActorSynchronizationContext()
        {
            return context.Context;
        }

        public DateTimeOffset Now
        {
            get { return ((IScheduler)context).Now; }
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return context.Schedule(state, dueTime, action);
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return context.Schedule(state, dueTime, action);
        }

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            return context.Schedule(state, action);
        }
    }

}
