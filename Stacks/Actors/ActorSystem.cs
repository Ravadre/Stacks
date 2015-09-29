using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors.CodeGen;

// ReSharper disable InconsistentNaming

namespace Stacks.Actors
{
    public class ActorSystem
    {
        public static ActorSystem Default { get; private set; }

        static ActorSystem()
        {
            Default = new ActorSystem("default");
        }

        public string SystemName { get; }

        private ConcurrentDictionary<string, IActor> registeredActors;
        private string autoGenActorName;

        internal ActorSystem(string systemName)
        {
            SystemName = systemName;
            Initialize();
        }

        /// <summary>
        /// Resets whole actor system by removing any references to existing actors.
        /// </summary>
        public void ResetSystem()
        {
            Initialize();
        }

        private void Initialize()
        {
            registeredActors = new ConcurrentDictionary<string, IActor>();
            CreateActor<IRootActor, RootActor>("root");
            autoGenActorName = "$a";
        }

        private Type GuessActorInterfaceType<T>()
        {
            var t = typeof(T);
            var implementedInterfaces = typeof(T).GetInterfaces();

            var matchedInterface = implementedInterfaces.FirstOrDefault(
                ii => ii.Name.Equals(t.Name, StringComparison.InvariantCultureIgnoreCase) ||
                      ii.Name.Equals("I" + t.Name, StringComparison.InvariantCultureIgnoreCase));

            if (matchedInterface != null)
                return matchedInterface;

            throw new Exception(string.Format("When creating an actor and providing only implementation type, " +
                "this type must implement a contract interface. Interface name must follow one of conventions: \r\n" +
                " - It must have the same name as implementation\r\n" +
                " - It must begin with \"I\" followed by implementation name"));
        }

        public IActor CreateActor<T>(Func<T> implementationProvider, string name = null, IActor parent = null)
            where T : Actor
        {
            var interfaceType = GuessActorInterfaceType<T>();
            return (IActor)CreateActor(interfaceType, implementationProvider, name, parent);
        }

        public IActor CreateActor<T>(string name = null, IActor parent = null)
            where T : Actor, new()
        {
            var interfaceType = GuessActorInterfaceType<T>();
            return (IActor)CreateActor(interfaceType, () => new T(), name, parent);
        }


        /// <summary>
        /// Creates a new actor, using I as an interface and TImpl as an implementation.
        /// I must be an interface. TImpl must implement this interface. 
        /// TImpl is an actual implementation that will be created, therefore it must support empty constructor.
        /// Name is optional, however, empty or null name will result in not registereing created actor in the actor system.
        /// </summary>
        /// <typeparam name="TImpl">Actor implementation type.</typeparam>
        /// <typeparam name="I">Actor interface.</typeparam>
        /// <param name="parent">Reference to a parent actor.</param>
        /// <param name="name">Optional name. Only named actors are registered to the system.</param>
        /// <returns></returns>
        public I CreateActor<I, TImpl>(string name = null, IActor parent = null)
            where TImpl : Actor, I, new()
        {
            return (I)CreateActor(typeof(I), () => new TImpl(), name, parent);
        }

        /// <summary>
        /// Creates a new actor, using I as an interface. implementation passed as a parameter is used as an implementation.
        /// I must be an interface. TImpl must implement this interface. 
        /// TImpl is an actual implementation that will be created, therefore it must support empty constructor.
        /// Name is optional, however, empty or null name will result in not registereing created actor in the actor system.
        /// </summary>
        /// <typeparam name="TImpl">Actor implementation type.</typeparam>
        /// <typeparam name="I">Actor interface</typeparam>
        /// <param name="implementationProvider">Actual implementation of an actor. Must inherit from Actor class.</param>
        /// <param name="parent">Reference to a parent actor.</param>
        /// <param name="name">Optional name. Only named actors are registered to the system.</param>
        /// <returns></returns>
        public I CreateActor<I, TImpl>(Func<TImpl> implementationProvider, string name = null, IActor parent = null)
            where TImpl: Actor, I
        {
            return (I)CreateActor(typeof (I), implementationProvider, name, parent);
        }

        /// <summary>
        /// Returns reference to already created actor. Actors that were created without name can not be accessed.
        /// </summary>
        /// <typeparam name="I">Actor's interface type.</typeparam>
        /// <param name="path">Path for the actor. /root/ can be ommited. Required.</param>
        /// <returns></returns>
        public I GetActor<I>(string path)
            where I: class
        {
            Ensure.IsNotNull(path, nameof(path));

            path = PathUtils.FixQueryPath(path);

            IActor actor;
            if (!registeredActors.TryGetValue(path, out actor))
            {
                throw new Exception(
                    $"Could not get actor with path {path}. It was not previously created in system {SystemName}");
            }

            var actorTyped = actor as I;
            if (actorTyped == null)
            {
                throw new Exception(
                    $"Received actor {path} in system {SystemName}. However, it does not implement interface {typeof (I).FullName}");
            }

            return actorTyped;
        }


        /// <summary>
        /// Returns reference to an already created actor. If actor with this name is not present, null is returned.
        /// <para></para>
        /// </summary>
        /// <typeparam name="I">Actor's interface type.</typeparam>
        /// <param name="path">Full path for an actor. /root/ can be ommited. Required.</param>
        /// <returns></returns>
        public I TryGetActor<I>(string path)
            where I : class
        {
            Ensure.IsNotNull(path, nameof(path));

            path = PathUtils.FixQueryPath(path);

            IActor actor;
            if (!registeredActors.TryGetValue(path, out actor))
            {
                return null;
            }

            return actor as I;
        }

        private object CreateActor<T>(Type interfaceType, Func<T> implementationProvider, string name, IActor parent)
            where T: Actor
        {
            Ensure.IsNotNull(interfaceType, nameof(interfaceType));
            Ensure.IsNotNull(implementationProvider, nameof(implementationProvider));

            if (parent == null && name != "root")
            {
                parent = GetActor<IRootActor>("root");
            }

            PathUtils.AssertNameForInvalidCharacters(name);

            if (name == null)
            {
                name = GenerateActorName();
            }

            var actorImplementation = ResolveImplementationProvider(implementationProvider);
            var actorWrapper = CreateActorWrapper(actorImplementation, interfaceType);

            try
            {
                var path = PathUtils.GetActorPath(parent, name);
                RegisterActorToSystem(actorWrapper, path);
                SetActorProperties(actorImplementation, actorWrapper, parent, name, path);
                actorImplementation.Start();
            }
            catch (Exception)
            {
                KillActor(actorImplementation);
                throw;
            }
            

            return actorWrapper;
        }

        private string GenerateActorName()
        {
            lock (autoGenActorName)
            {
                var n = autoGenActorName;
                var prefix = n.Substring(0, n.Length - 1);
                var x = n[n.Length - 1];
                x = (char) (x + 1);
                autoGenActorName = x > 'z' ? prefix + "aa" : prefix + x;
                return autoGenActorName;
            }
        }

        private static IActor TryUnwrapActorAsWrapper(IActor actor)
        {
            if (actor == null) return null;
            if (actor is ActorWrapperBase) return actor;
            return (actor as Actor)?.Wrapper;
        }

        private void SetActorProperties(Actor actor, IActor actorWrapper, IActor parent, string name, string path)
        {
            parent = TryUnwrapActorAsWrapper(parent);
            var parentWrapper = parent as ActorWrapperBase;

            actor.SetName(name);
            actor.SetPath(path);
            actor.SetParent(parent);
            actor.SetActorSystem(this);
            actor.SetWrapper(actorWrapper);
            if (parent != null)
            {
                if (parentWrapper == null)
                {
                    throw new Exception(
                        $"Could not cast parent actor {parent.GetType().FullName} to ActorWrapperBase");
                }

                parentWrapper.ActorImplementation.AddChild(actorWrapper);
            }
        }

        private IActor CreateActorWrapper(object actorImplementation, Type actorInterface)
        {
            var typeGenerator = new ActorTypeGenerator();
            var wrapperType = typeGenerator.GenerateType(actorImplementation, actorInterface);
            var wrapperObject = Activator.CreateInstance(wrapperType, actorImplementation) as IActor;

            if (wrapperObject == null)
            {
                throw new Exception("Internal error occured when creating wrapper for implementation of " 
                    + actorImplementation.GetType().FullName + ", interface type: " 
                    + actorInterface.FullName);
            }

            return wrapperObject;
        }

        private TImpl ResolveImplementationProvider<TImpl>(Func<TImpl> implementationProvider)
        {
            try
            {
                ActorCtorGuardian.SetGuard();
                return implementationProvider();
            }
            finally
            {
                ActorCtorGuardian.ClearGuard();
            }
        }

        private void RegisterActorToSystem(IActor actor, string path)
        {
            Ensure.IsNotNull(actor, nameof(actor));
            Ensure.IsNotNull(path, nameof(path));

            if (!registeredActors.TryAdd(path, actor))
            {
                throw new Exception(
                    $"Tried to create actor named {path} inside system {SystemName}. Actor with such name is already added");
            }
        }
        
        internal void KillActor(Actor actor)
        {
            Ensure.IsNotNull(actor, nameof(actor));

            if (actor.Name != null)
            {
                IActor a;
                registeredActors.TryRemove(actor.Path, out a);
            }

            (actor.Parent as ActorWrapperBase)?.ActorImplementation.RemoveChild(actor.Wrapper);
            actor.SetParent(null);
        }
    }
}
