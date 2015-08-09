using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.ActorSystemTests
{
    public class ActorTests
    {
        public ActorTests()
        {
            ActorSystem.Default.ResetSystem();
        }

        [Fact]
        public async Task OnStart_should_be_called_for_created_actor()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, OnStartActor>("ac");

            Assert.Equal(2, await actor.Div(2, 6));
        }

        [Fact]
        public void If_OnStart_throws_CreateActor_should_throw()
        {
            Assert.Throws<Exception>(() =>
            {
                var actor = ActorSystem.Default.CreateActor<ICalculatorActor, ThrowsOnStartActor>("ac");
            });
        }

        [Fact]
        public void If_OnStart_throws_actor_should_not_be_registered_as_a_child_to_root()
        {
            try
            {
                var actor = ActorSystem.Default.CreateActor<ICalculatorActor, ThrowsOnStartActor>("ac");
            }
            catch
            {}

            var root = ActorSystem.Default.GetActor<IRootActor>("root");
            Assert.Equal(0, root.Childs.Count());
        }

        [Fact]
        public void If_OnStart_throws_actor_should_not_be_available_in_system()
        {
            try
            {
                var actor = ActorSystem.Default.CreateActor<ICalculatorActor, ThrowsOnStartActor>("ac");
            }
            catch
            { }

            Assert.Throws<Exception>(() =>
            {
                var ac = ActorSystem.Default.GetActor<ICalculatorActor>("ac");
            });

        }
    }

    public class ThrowsOnStartActor : Actor, ICalculatorActor
    {
        protected override async Task OnStart()
        {
            await Context;

            throw new Exception("test");
        }

        public async Task<double> Div(double x, double y)
        {
            await Context;
            return 5;
        }
    }

    public class OnStartActor : Actor, ICalculatorActor
    {
        private int offset = 0;

        public async Task<double> Add(double x, double y)
        {
            await Context;
            return x + y + offset;
        }

        protected override async Task OnStart()
        {
            await Context;

            offset = 10;
        }

        public async Task<double> Div(double x, double y)
        {
            await Context;

            return (x + offset) / y;
        }
    }
}
