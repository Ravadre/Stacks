using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public interface IDependencyResolverHelper
    {
        T Resolve<T>(string name = null, IActor parent = null);
        T Resolve<T>(IDictionary<string, object> args, string name = null, IActor parent = null);

        T Resolve<T>(string resolveName, IDictionary<string, object> args = null, string name = null,
            IActor parent = null);
        void Release<T>(T obj);
    }

    class DependencyResolverHelper : IDependencyResolverHelper
    {
        private readonly ActorSystem actorSystem;

        public DependencyResolverHelper(ActorSystem actorSystem)
        {
            this.actorSystem = actorSystem;
        }

        public T Resolve<T>(string name = null, IActor parent = null)
        {
            return actorSystem.CreateActor(() => actorSystem.DependencyResolver.Resolve<T>(null, null), name, parent);
        }

        public T Resolve<T>(IDictionary<string, object> args, string name = null, IActor parent = null)
        {
            return actorSystem.CreateActor(() => actorSystem.DependencyResolver.Resolve<T>(null, args), name, parent);
        }

        public T Resolve<T>(string resolveName, IDictionary<string, object> args = null, string name = null, IActor parent = null)
        {
            return actorSystem.CreateActor(() => actorSystem.DependencyResolver.Resolve<T>(resolveName, args), name, parent);
        }

        public void Release<T>(T obj)
        {
            actorSystem.DependencyResolver.Release(obj);
        }
    }
}
