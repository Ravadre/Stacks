using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

namespace Stacks.Actors
{
    public class ActorSystem
    {
        public static ActorSystem Default { get; private set; }

        static ActorSystem()
        {
            Default = new ActorSystem("Default");
        }

        public string Name { get; private set; }

        private readonly ConcurrentDictionary<string, IActor> registeredActors;

        internal ActorSystem(string name)
        {
            Name = name;
            registeredActors = new ConcurrentDictionary<string, IActor>();
        }

        /// <summary>
        /// Creates a new actor, using I as an interface and TImpl as an implementation.
        /// I must be an interface. TImpl must implement this interface. 
        /// TImpl is an actual implementation that will be created, therefore it must support empty constructor.
        /// Name is optional, however, empty or null name will result in not registereing created actor in the actor system.
        /// </summary>
        /// <typeparam name="I">Actor interface.</typeparam>
        /// <typeparam name="TImpl">Actor implementation type.</typeparam>
        /// <param name="name">Optional name. Only named actors are registered to the system.</param>
        /// <returns></returns>
        public I CreateActor<I, TImpl>(string name = null)
            where TImpl : class, I, new()
        {
            return CreateActor<I, TImpl>(new TImpl(), name);
        }

        /// <summary>
        /// Creates a new actor, using I as an interface. implementation passed as a parameter is used as an implementation.
        /// I must be an interface. TImpl must implement this interface. 
        /// TImpl is an actual implementation that will be created, therefore it must support empty constructor.
        /// Name is optional, however, empty or null name will result in not registereing created actor in the actor system.
        /// </summary>
        /// <typeparam name="I">Actor interface</typeparam>
        /// <typeparam name="TImpl">Actor implementation type.</typeparam>
        /// <param name="actorImplementation">Actual implementation of an actor. Must inherit from Actor class.</param>
        /// <param name="name">Optional name. Only named actors are registered to the system.</param>
        /// <returns></returns>
        public I CreateActor<I, TImpl>(TImpl actorImplementation, string name = null)
            where TImpl: class, I
        {
            Ensure.IsNotNull(actorImplementation, "actorImplementation");
            EnsureInheritsActor<TImpl>(); 
            
            var implementationAsActor = CastImplementationToActor(actorImplementation);
            RegisterActorToSystem(implementationAsActor, name);

            return actorImplementation;
        }

        private Actor CastImplementationToActor<TImpl>(TImpl actorImplementation)
        {
            var implementationAsActor = actorImplementation as Actor;

            if (implementationAsActor == null)
                throw new Exception("Created implementation could not be casted to Stacks.Actors.Actor type.");

            return implementationAsActor;
        }

        private void RegisterActorToSystem(Actor actorImplementation, string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (!registeredActors.TryAdd(name, actorImplementation))
            {
                throw new Exception(string.Format("Tried to create actor named {0} inside system {1}. Actor with such name is already added",
                    name, this.Name));
            }
        }

        private static void EnsureInheritsActor<TImpl>()
        {
            if (!typeof(Actor).IsAssignableFrom(typeof(TImpl)))
                throw new ArgumentException(
                    string.Format(
                        "Implementation type (TImpl) is of type {0} which does not inherits from Stacks.Actors.Actor.",
                        typeof(TImpl).FullName));
        }
    }
}
