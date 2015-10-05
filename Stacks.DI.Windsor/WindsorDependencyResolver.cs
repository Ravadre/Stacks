using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
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

        IDictionary GetArgs(IDictionary<string, object> arguments)
        {
            if (arguments == null)
                return null;

            var args = arguments as IDictionary;

            if (args != null)
            {
                return args;
            }
            else
            {
                var dic = new Dictionary<string, object>(arguments);
                return dic;
            }
        }

        public T Resolve<T>(string resolverKey, IDictionary<string, object> arguments)
        {
            if (resolverKey == null)
            {
                return container.Resolve<T>(GetArgs(arguments));
            }
            else
            {
                return container.Resolve<T>(resolverKey, GetArgs(arguments));
            }
        }

        public void Release<T>(T actor)
        {
            container.Release(actor);
        }
    }
}
