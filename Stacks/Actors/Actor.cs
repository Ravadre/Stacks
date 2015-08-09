using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public abstract class Actor : IActor
    {
        private readonly ActorContext context;

        public IActor Parent { get; private set; }
        public IEnumerable<IActor> Childs => childs.Keys;
         
        private readonly ConcurrentDictionary<IActor, IActor> childs; 

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

            childs = new ConcurrentDictionary<IActor, IActor>();

            context = new ActorContext(this, executor);
        }

        internal void SetName(string newName)
        {
            Name = newName;
            context.SetName(newName);
        }

        internal void SetParent(IActor parentActor)
        {
            Parent = parentActor;
        }

        internal void AddChild(IActor childActor)
        {
            Ensure.IsNotNull(childActor, nameof(childActor));

            if (!childs.TryAdd(childActor, childActor))
            {
                throw new InvalidOperationException($"Tried to add child to actor '{Name}' - {GetType().FullName}. " + 
                    $"Child to be added '{childActor.Name}' - {childActor.GetType().FullName}. Actor already has this child registered.");   
            }
        }

        protected IActorContext Context => context;
        protected SynchronizationContext GetActorSynchronizationContext() => context.SynchronizationContext;
        protected Task Completion => context.Completion;
        protected Task Stop() => context.Stop();

        public bool Named => Name != null;
        public string Name { get; private set; }
    }
}