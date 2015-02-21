using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public abstract class Actor : IActor, IScheduler
    {
        private readonly ActorContext context;
        private string name;

        protected Actor()
            : this(new ActionBlockExecutor(ActionBlockExecutorSettings.Default))
        { }

        protected Actor(ActorSettings settings)
            : this(
                new ActionBlockExecutor(
                    ActionBlockExecutorSettings.DefaultWith(settings.SupportSynchronizationContext)))
        { }

        private Actor(IExecutor executor)
        {
            this.context = new ActorContext(executor);
        }
        
        internal void SetName(string name)
        {
            this.name = name;
            context.SetName(name);
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
            return context.SynchronizationContext;
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
