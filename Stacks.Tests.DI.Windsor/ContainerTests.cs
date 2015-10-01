using System.Threading.Tasks;
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
            ActorSystem.Default.DependencyResolver = new WindsorDependencyResolver(container);
            Service1.ctr = 0;
        }

        [Fact]
        public void Actor_without_dependencies_should_be_resolved()
        {
            container.Register(
                Component.For<ICalculatorActor>().ImplementedBy<CalculatorActor>()
            );

            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>();
        }

        [Fact]
        public void Actor_registered_with_key_should_be_resolved()
        {
            container.Register(
                Component.For<IDataActor>().ImplementedBy<DataActor>()
            );

            var actor = ActorSystem.Default.CreateActor<IDataActor, DataActor>(new Args { ["data"] = "a" });
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
            container.Register(
                Component.For<IDepActor>().ImplementedBy<DepActor>(),
                Component.For<IService1>().ImplementedBy<Service1>()
            );

            var actor = ActorSystem.Default.CreateActor<IDepActor, DepActor>();
            var actor2 = ActorSystem.Default.CreateActor<IDepActor, DepActor>();
            Assert.Equal(1, actor.ServiceCounter().Result);
            Assert.Equal(1, actor2.ServiceCounter().Result);
        }

        [Fact]
        public void Resolving_transient_service_should_create_new_instances()
        {
            container.Register(
                Component.For<IDepActor>().ImplementedBy<DepActor>().LifestyleTransient(),
                Component.For<IService1>().ImplementedBy<Service1>().LifestyleTransient()
            );

            var actor = ActorSystem.Default.CreateActor<IDepActor, DepActor>();
            var actor2 = ActorSystem.Default.CreateActor<IDepActor, DepActor>();
            Assert.Equal(2, actor.ServiceCounter().Result);
            Assert.Equal(2, actor2.ServiceCounter().Result);

            Assert.Equal("$b", actor.Name);
            Assert.Equal("$c", actor2.Name);
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
