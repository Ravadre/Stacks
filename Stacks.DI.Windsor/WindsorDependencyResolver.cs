using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using Castle.MicroKernel.ModelBuilder.Descriptors;
using Castle.MicroKernel.Registration;
using Castle.Windsor;

namespace Stacks.Actors.DI.Windsor
{
    public class WindsorDependencyResolver : IDependencyResolver
    {
        private readonly ActorSystem actorSystem;
        private readonly IWindsorContainer container;

        public WindsorDependencyResolver(ActorSystem actorSystem, IWindsorContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            this.actorSystem = actorSystem;
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

        public void Register<I, TImpl>()
            where I: class
            where TImpl: I
        {
            container.Register(
                Component.For<I>().UsingFactoryMethod(
                    (kernel, model, ctx) =>
                    {
                        return actorSystem.CreateActor(() => kernel.Resolve<I>("$Stacks$Internal$Registration$" + typeof(TImpl).FullName, ctx.AdditionalArguments), null, null);
                    }),
                Component.For<I>().ImplementedBy<TImpl>().Named("$Stacks$Internal$Registration$" + typeof(TImpl).FullName)
                );
        }

        public void RegisterTransient<I, TImpl>()
          where I : class
          where TImpl : I
        {
            container.Register(
                Component.For<I>().UsingFactoryMethod(
                    (kernel, model, ctx) =>
                    {
                        return actorSystem.CreateActor(() => kernel.Resolve<I>("$Stacks$Internal$Registration$" + typeof(TImpl).FullName, ctx.AdditionalArguments), null, null);
                    }).LifestyleTransient(),
                Component.For<I>().ImplementedBy<TImpl>().Named("$Stacks$Internal$Registration$" + typeof(TImpl).FullName).LifestyleTransient()
                );
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
