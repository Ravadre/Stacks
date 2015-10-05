using System;
using System.Collections.Generic;

namespace Stacks.Actors.DI
{
    class DependencyInjectionHelper : IDependencyInjectionHelper
    {
        private readonly ActorSystem actorSystem;

        public DependencyInjectionHelper(ActorSystem actorSystem)
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

        public T Resolve<T>()
        {
            return Resolve<T>(null, null);
        }

        public T Resolve<T>(IDictionary<string, object> args)
        {
            return Resolve<T>(null, args);
        }

        public T Resolve<T>(string resolveName, IDictionary<string, object> args = null)
        {
            var resolver = GetResolverOrFail();
            return resolver.Resolve<T>(resolveName, args);
        }

        public void Release<T>(T obj)
        {
            var resolver = GetResolverOrFail();
            resolver.Release(obj);
        }

        public void Register<I, TImpl>()
            where I : class
            where TImpl : Actor, I
        {
            var resolver = GetResolverOrFail();
            resolver.Register<I, TImpl>();
        }

        public void RegisterTransient<I, TImpl>()
            where I : class
            where TImpl : Actor, I
        {
            var resolver = GetResolverOrFail();
            resolver.RegisterTransient<I, TImpl>();
        }
    }
}