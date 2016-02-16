using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors.CodeGen;
using Stacks.Actors.DI;

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

        public IDependencyInjectionHelper DI { get; private set; }

        private ConcurrentDictionary<string, IActor> registeredActors;
        private string autoGenActorName;
        private volatile IDependencyResolver dependencyResolver;

        internal IDependencyResolver DependencyResolver => dependencyResolver;

        public void SetDependencyResolver(IDependencyResolver dependencyResolver)
        {
            this.dependencyResolver = dependencyResolver;
        }

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
            try
            {
                GetActor<IRootActor>("root").Stop().Wait();
            }
            catch
            {
                // Ignore
            }
            
            Initialize();
        }

        private void Initialize()
        {
            DI = new DependencyInjectionHelper(this);
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

        private MethodInfo GetCreateMethodForGuessedInterface<T>(Type interfaceType)
        {
            return GetType()
                .GetMethods()
                .First(
                    m =>
                        m.Name == "CreateActor" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2 &&
                        m.GetParameters().Length == 3)
                .MakeGenericMethod(interfaceType, typeof (T));
        }

        public IActor CreateActor<T>(object[] args, string name = null, IActor parent = null)
          where T : Actor
        {
            var interfaceType = GuessActorInterfaceType<T>();
            return (IActor)(GetCreateMethodForGuessedInterface<T>(interfaceType)
                            .Invoke(this, new object[] { args, name, parent }));
        }
        
        public IActor CreateActor<T>(string name = null, IActor parent = null)
            where T : Actor, new()
        {
            var interfaceType = GuessActorInterfaceType<T>();
            return (IActor)(GetCreateMethodForGuessedInterface<T>(interfaceType)
                            .Invoke(this, new object[] { null, name, parent }));
        }

        public I CreateActor<I, TImpl>(object[] args, string name = null, IActor parent = null)
           where TImpl : Actor, I
        {
            if (args == null)
            {
                args = new object[0];
            }

            return CreateActor(() => (I)Activator.CreateInstance(typeof(TImpl), args), name, parent);
        }
        
        /// <summary>
        /// Creates a new actor, using I as an interface.
        /// I must be an interface. TImpl must implement this interface. 
        /// TImpl is an actual implementation that will be created.
        /// Name is optional.
        /// </summary>
        /// <typeparam name="TImpl">Actor implementation type.</typeparam>
        /// <typeparam name="I">Actor interface</typeparam>
        /// <param name="parent">Reference to a parent actor.</param>
        /// <param name="name">Optional name. Only named actors are registered to the system.</param>
        /// <returns></returns>
        public I CreateActor<I, TImpl>(string name = null, IActor parent = null)
            where TImpl: Actor, I, new()
        {
            return CreateActor<I>(() => new TImpl(), name, parent);
        }

        public I CreateActor<I>(Func<I> actorProvider, string name, IActor parent)
        {
            if (parent == null && name != "root")
            {
                parent = GetActor<IRootActor>("root");
            }

            PathUtils.AssertNameForInvalidCharacters(name);

            if (name == null)
            {
                name = GenerateActorName();
            }

            var actorImplementation = ResolveActorImplementation(actorProvider);
            var actor = ValidateCreatedActorInstance<I>(actorImplementation);
            var actorWrapper = CreateActorWrapper(actorImplementation, typeof(I));

            try
            {
                var path = PathUtils.GetActorPath(parent, name);
                RegisterActorToSystem(actorWrapper, path);
                SetActorProperties(actor, actorWrapper, parent, name, path);
                actor.Start();
            }
            catch (Exception)
            {
                UnregisterActor(actor);
                throw;
            }


            return (I)actorWrapper;
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


        private Actor ValidateCreatedActorInstance<I>(object actorImplementation)
        {
            Ensure.IsNotNull(actorImplementation, nameof(actorImplementation));

            var actor = actorImplementation as Actor;

            if (actor == null)
                throw new Exception($"Object created by actor implementation does not inherit from {nameof(Actor)} class.\r\n" + 
                    $"Actor interface: {typeof(I).Name}. Object type: {actorImplementation.GetType().Name}");

            return actor;
        }

        internal static string GenerateActorName(string lastName)
        {
            var n = lastName;
            var idx = n.Length - 1;

            while (idx >= 0)
            {
                var c = n[idx];
                if (char.IsLetter(c))
                {
                    if (c != 'z')
                    {
                        var x = (char) (c + 1);
                        return n.Substring(0, idx) + x + new string('a', n.Length - idx - 1);
                    }
                }
                else
                {
                    return n.Substring(0, idx + 1) + new string('a', n.Length);
                }

                --idx;
            }

            return "a" + n;
        }

        private string GenerateActorName()
        {
            lock (autoGenActorName)
            {
                return autoGenActorName = GenerateActorName(autoGenActorName);
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

        private I ResolveActorImplementation<I>(Func<I> actorProvider)
        {
            try
            {
                ActorCtorGuardian.SetGuard();

                return actorProvider();
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
        
        internal void UnregisterActor(Actor actor)
        {
            Ensure.IsNotNull(actor, nameof(actor));

            if (actor.Name != null)
            {
                IActor a;
                registeredActors.TryRemove(actor.Path, out a);
            }

            (actor.Parent as ActorWrapperBase)?.ActorImplementation.RemoveChild(actor.Wrapper);
            actor.SetParent(null);

            dependencyResolver?.Release(actor);
        }
    }
}
