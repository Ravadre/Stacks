using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    public interface ICalculatorActor
    {
        Task<double> Div(double x, double y);
    }

    public interface ISingleMethodActor
    {
        Task<int> Test();
    }

    public class SingleMethodActor : Actor, ISingleMethodActor
    {
        public async Task<int> Test()
        {
            await Context;

            return 5;
        }
    }

    public class CalculatorActor : Actor, ICalculatorActor
    {
        public async Task<double> Div(double x, double y)
        {
            await Context;

            return x / y;
        }
    }
}
