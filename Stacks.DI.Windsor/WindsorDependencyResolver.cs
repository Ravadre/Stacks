using System;
using System.Collections;
using Castle.Windsor;

namespace Stacks.Actors.DI.Windsor
{
    public class WindsorDependencyResolver : IDependencyResolver
    {
        private readonly IWindsorContainer container;

        public WindsorDependencyResolver(IWindsorContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            this.container = container;
        }

        public object Resolve<T>(Type interfaceType, string resolverKey, IDictionary arguments)
        {
            if (resolverKey == null)
            {
                return container.Resolve(interfaceType, arguments);
            }
            else
            {
                return container.Resolve(resolverKey, interfaceType, arguments);
            }
        }

        public void Release(IActor actor)
        {
            container.Release(actor);
        }
    }
}
