using System.Collections.Generic;

namespace Stacks.Actors.DI
{
    public interface IDependencyResolver
    {
        void Register<I, TImpl>()
            where I : class
            where TImpl : I;

        void RegisterTransient<I, TImpl>()
            where I : class
            where TImpl : I;

        T Resolve<T>(string resolverKey, IDictionary<string, object> arguments);
        void Release<T>(T actor);
        
    }
}