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
            ActorSystem.Default.DependencyResolver = new DependencyResolver(container);
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
