using System;
using System.Linq;
using System.Threading.Tasks;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Stacks.Actors.DI;
using Stacks.Actors.DI.Windsor;
using Stacks.Tests.ActorSystemTests;
using Xunit;

namespace Stacks.Actors.Tests.DI.Windsor
{
    public class ContainerTests
    {
        private readonly IWindsorContainer container;

        public ContainerTests()
        {
            container = new WindsorContainer();
            
            ActorSystem.Default.ResetSystem();
            ActorSystem.Default.SetDependencyResolver(new WindsorDependencyResolver(ActorSystem.Default, container));
            Service1.ctr = 0;
        }

        [Fact]
        public void Actor_without_dependencies_should_be_resolved()
        {
            ActorSystem.Default.DI.Register<ICalculatorActor, CalculatorActor>();
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>();
        }

        [Fact]
        public void Actor_registered_with_key_should_be_resolved()
        {
            ActorSystem.Default.DI.Register<IDataActor, DataActor>();
            
            var actor = ActorSystem.Default.DI.Resolve<IDataActor>(new Args { ["data"] = "a" });
            Assert.Equal("a", actor.GetData().Result);
        }


        [Fact]
        public void Resolving_without_arguments_should_pick_up_parameterless_constructor()
        {
            container.Register(
                Component.For<IDataActor>().ImplementedBy<DataActor>()
            );

            var actor = ActorSystem.Default.CreateActor<IDataActor, DataActor>();
            Assert.Equal("default", actor.GetData().Result);
        }

        [Fact]
        public void Resolving_should_pickup_dependent_services()
        {
            ActorSystem.Default.DI.Register<IDepActor, DepActor>();
            container.Register(
                Component.For<IService1>().ImplementedBy<Service1>()
            );

            var actor = ActorSystem.Default.DI.Resolve<IDepActor>();
            var actor2 = ActorSystem.Default.DI.Resolve<IDepActor>();
            Assert.Equal(1, actor.ServiceCounter().Result);
            Assert.Equal(1, actor2.ServiceCounter().Result);
        }

        [Fact]
        public void Resolving_transient_service_should_create_new_instances()
        {
            ActorSystem.Default.DI.RegisterTransient<IDepActor, DepActor>();

            container.Register(
                Component.For<IService1>().ImplementedBy<Service1>().LifestyleTransient()
            );

            var actor = ActorSystem.Default.DI.Resolve<IDepActor>();
            var actor2 = ActorSystem.Default.DI.Resolve<IDepActor>();
            Assert.Equal(2, actor.ServiceCounter().Result);
            Assert.Equal(2, actor2.ServiceCounter().Result);

            Assert.Equal("$b", actor.Name);
            Assert.Equal("$c", actor2.Name);
        }

        [Fact]
        public void When_service_is_resolved_with_actor_as_an_dependency_wrapper_should_be_returned()
        {
            ActorSystem.Default.DI.Register<IDepActor, DepActor>();

            container.Register(
                Component.For<IService1>().ImplementedBy<Service1>(),
                Component.For<IService2>().ImplementedBy<Service2>()
            );

            var c = container.Resolve<IService2>();
            var ac = container.Resolve<IDepActor>();
            var ac2 = ActorSystem.Default.DI.Resolve<IDepActor>();

            Assert.True(ReferenceEquals(ac, ac2));
            Assert.True(ReferenceEquals(c.Actor, ac));
        }
    }

    public interface IService2
    {
        IDepActor Actor { get; }
    }

    public class Service2 : IService2
    {
        public IDepActor Actor { get; }

        public Service2(IDepActor actor)
        {
            Actor = actor;
        }
    }

    public interface IService1
    {
        int InstanceCounter();
    }

    public class Service1 : IService1
    {
        public static volatile int ctr = 0;

        public int InstanceCounter()
        {
            return ctr;
        }

        public Service1()
        {
            ++ctr;
        }
    }

    public interface IDepActor : IActor
    {
        Task<int> ServiceCounter();
    }

    public class DepActor : Actor, IDepActor
    {
        private readonly IService1 service;

        public DepActor(IService1 service)
        {
            this.service = service;
        }

        public async Task<int> ServiceCounter()
        {
            await Context;

            return service.InstanceCounter();
        }
    }

    public interface IDataActor
    {
        Task<string> GetData();
    }

    public class DataActor : Actor, IDataActor
    {
        private readonly string data;

        public DataActor()
            : this("default")
        {
            
        }

        public DataActor(string data)
        {
            this.data = data;
        }

        public Task<string> GetData()
        {
            return Context.PostTask(() => data);
        }
    }
}
