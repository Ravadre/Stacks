using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.ActorSystemTests
{
    public class ActorCodeGeneration
    {
        public ActorCodeGeneration()
        {
            ActorSystem.Default.ResetSystem();
        }

        [Fact]
        public void Actor_with_single_method_returning_task_should_compile_successfully()
        {
            var actor = ActorSystem.Default.CreateActor<ISingleMethodActor, SingleMethodActor>();
            Assert.Equal(5, actor.Test().Result);
        }

        [Fact]
        public void Actor_with_method_with_parameters_should_compile_successfully()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>();
            Assert.Equal(30, actor.Div(150, 5).Result);
        }

        [Fact]
        public async Task Actor_error_should_be_propagated_in_returned_task()
        {
            await Assert.ThrowsAsync<DivideByZeroException>(async () =>
            {
                var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>();
                var res = actor.Div(5.0, .0);
                
                await res;
            });
        }

        [Fact]
        public async Task
            Actor_that_has_explicitly_implemented_method_should_correctly_pass_method_call_to_implementation()
        {
            var actor = ActorSystem.Default.CreateActor<IExplicitInterfaceActor, ExplicitInterfaceActor>();

            var sum = await actor.Sum(new double[] {5, 4, 3, 2, 1});

            Assert.Equal(15, sum);
        }

        [Fact]
        public async Task Actor_Observable_that_has_explicit_implementation_should_be_properly_called()
        {
            var actor = ActorSystem.Default.CreateActor<IExplicitInterfaceActor, ExplicitInterfaceActor>();

            int ctr = 0;
            actor.Counter.Subscribe(_ => ++ctr);

            for (int i = 0; i < 10; ++i)
            {
                await Task.Delay(200);
                if (ctr >= 2) break;
            }

            Assert.True(ctr >= 2);
        }

        [Fact]
        public async Task Actor_with_extra_method_should_not_break_compiler()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, ActorWithExtraMethod>();

            Assert.Equal(15.0, await actor.Div(1.0, 1.0));
        }

        [Fact]
        public void Actor_through_IActor_interface_should_return_valid_actor_name()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, ActorWithExtraMethod>("Custom-actor-name");

            Assert.Equal("Custom-actor-name", actor.Name);
        }
    }

}
