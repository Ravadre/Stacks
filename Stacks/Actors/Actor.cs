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
        {
        }

        protected Actor(ActorSettings settings)
            : this(
                new ActionBlockExecutor(
                    ActionBlockExecutorSettings.DefaultWith(settings.SupportSynchronizationContext)))
        {
        }

        private Actor(IExecutor executor)
        {
            if (!ActorCtorGuardian.IsGuarded())
            {
                throw new Exception(
                    $"Tried to created actor of {GetType().FullName} using constructor. Please, use ActorSystem.CreateActor method instead.");
            }

            context = new ActorContext(executor);
        }

        internal void SetName(string name)
        {
            this.name = name;
            context.SetName(name);
        }

        protected IActorContext Context => context;
        protected SynchronizationContext GetActorSynchronizationContext() => context.SynchronizationContext;
        protected Task Completion => context.Completion;
        protected Task Stop() => context.Stop();

        public bool Named => name != null;
        public string Name => name;

        public DateTimeOffset Now => ((IScheduler) context).Now;

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime,
            Func<IScheduler, TState, IDisposable> action) => context.Schedule(state, dueTime, action);

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
            => context.Schedule(state, dueTime, action);

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
            => context.Schedule(state, action);
    }
}