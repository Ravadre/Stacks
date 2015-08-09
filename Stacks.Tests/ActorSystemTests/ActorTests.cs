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
            var actor = ActorSystem.Default.CreateActor<IImplementedActor, ImplementedActor>("ac");

            Assert.Equal(19, await actor.Add(3, 6));
        }
    }

    public class ImplementedActor : Actor, IImplementedActor
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
    }

    public interface IImplementedActor
    {
        Task<double> Add(double x, double y);
    }
}
