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
        private readonly IExecutor executor;
        private readonly ActorContext context;
        internal IActor Wrapper { get; private set; }

        public IActor Parent { get; private set; }
        public IEnumerable<IActor> Children => children.Keys;
        public ActorSystem System { get; private set; }

        private readonly ManualResetEventSlim isStoppingEvent;
        private readonly ManualResetEventSlim syncLock;
        private readonly ConcurrentDictionary<IActor, IActor> children;

       
        /// <summary>
        /// Constructor should NOT be used to initialize an actor, as it is still in process of creation and all
        /// dependencied may not be registered to ActorSystem. 
        /// <para></para>
        /// Use OnStart() method to instead.
        /// </summary>
        protected Actor()
            : this(new ActionBlockExecutor(ActionBlockExecutorSettings.Default))
        {
        }

        /// <summary>
        /// Constructor should NOT be used to initialize an actor, as it is still in process of creation and all
        /// dependencied may not be registered to ActorSystem. 
        /// <para></para>
        /// Use OnStart() method to instead.
        /// </summary>
        protected Actor(ActorSettings settings)
            : this(
                new ActionBlockExecutor(
                    ActionBlockExecutorSettings.DefaultWith(settings.SupportSynchronizationContext)))
        {
        }

        private Actor(IExecutor executor)
        {
            this.executor = executor;

            if (!ActorCtorGuardian.IsGuarded())
            {
                throw new Exception(
                    $"Tried to create an actor of type {GetType().FullName} using constructor. Please, use " + 
                    $"{nameof(ActorSystem)}.{nameof(ActorSystem.CreateActor)} method instead.");
            }

            children = new ConcurrentDictionary<IActor, IActor>();
            isStoppingEvent = new ManualResetEventSlim();
            syncLock = new ManualResetEventSlim(true);

            executor.Error += ErrorOccuredInExecutor;
            context = new ActorContext(executor);
        }

        private void ErrorOccuredInExecutor(Exception exn)
        {
            // This should probably never happen, if it does, always stop actor, as this can be considered fatal error.
            Stop().Wait();
        }

        internal void ErrorOccuredInMethod(string methodName, Exception exn)
        {
            Stop().Wait();
        }

        protected virtual void OnStart()
        {
        }
        
        protected virtual void OnStopped()
        {
        }

        internal void Start()
        {
            try
            {
                OnStart();
            }
            catch (Exception exn)
            {
                throw new Exception($"Error occured when actor was starting. Actor: '{Name}' - {GetType().FullName}. See inner exception for details.", exn);
            }
        }

        public Task Stop()
        {
            // To avoid deadlocks, stopping procedure is called on threadpool. Is it necessary?
            return Task.Run(() =>
            {
                isStoppingEvent.Set();
                syncLock.Wait();

                foreach (var child in Children.ToArray())
                {
                    child.Stop().Wait();
                }

                context.Stop().Wait();
                
                System.KillActor(this);

                try
                {
                    OnStopped();
                }
                catch (Exception)
                {
                    // ignore
                }
            });
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

            try
            {
                syncLock.Reset();
                if (isStoppingEvent.IsSet)
                {
                    throw new Exception();
                }

                if (!children.TryAdd(childActor, childActor))
                {
                    throw new InvalidOperationException(
                        $"Tried to add child to actor '{Name}' - {GetType().FullName}. " +
                        $"Child to be added '{childActor.Name}' - {childActor.GetType().FullName}. Actor already has this child registered.");
                }
            }
            finally
            {
                syncLock.Set();
            }
        }

        internal void SetWrapper(IActor actorWrapper)
        {
            Wrapper = actorWrapper;
        }
        
        internal void SetActorSystem(ActorSystem system)
        {
            System = system;
        }

        internal void RemoveChild(IActor childActor)
        {
            Ensure.IsNotNull(childActor, nameof(childActor));
            IActor a;
            children.TryRemove(childActor, out a);
        }

        protected IActorContext Context => context;
        protected SynchronizationContext GetActorSynchronizationContext() => context.SynchronizationContext;
        protected Task Completion => context.Completion;
       
        public bool Named => Name != null;
        public string Name { get; private set; }

    }
}