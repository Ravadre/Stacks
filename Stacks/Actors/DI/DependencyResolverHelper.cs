using System;
using System.Collections.Generic;

namespace Stacks.Actors.DI
{
    class DependencyResolverHelper : IDependencyResolverHelper
    {
        private readonly ActorSystem actorSystem;

        public DependencyResolverHelper(ActorSystem actorSystem)
        {
            this.actorSystem = actorSystem;
        }

        private IDependencyResolver GetResolverOrFail()
        {
            var resolver = actorSystem.DependencyResolver;

            if (resolver == null)
                throw new Exception($"Cannot use dependency injection ({nameof(ActorSystem)}.{nameof(ActorSystem.DI)}) before setting " + 
                    $"dependency resolver through {nameof(ActorSystem.SetDependencyResolver)} method");

            return resolver;
        }

        public T Resolve<T>(string name = null, IActor parent = null)
        {
            return Resolve<T>(null, null, name, parent);
        }

        public T Resolve<T>(IDictionary<string, object> args, string name = null, IActor parent = null)
        {
            return Resolve<T>(null, args, name, parent);
        }

        public T Resolve<T>(string resolveName, IDictionary<string, object> args = null, string name = null, IActor parent = null)
        {
            var resolver = GetResolverOrFail();
            return actorSystem.CreateActor(() => resolver.Resolve<T>(resolveName, args), name, parent);
        }

        public void Release<T>(T obj)
        {
            actorSystem.DependencyResolver.Release(obj);
        }
    }
}