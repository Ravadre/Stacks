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
            var t = actor.Test();
            Assert.Equal(5, actor.Test().Result);
        }
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
}
