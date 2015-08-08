using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.ActorSystemTests
{
    public class HierarchyTests
    {
        [Fact]
        public void Actor_without_parent_should_have_RootActor_set_as_parent()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("calc");

            Assert.Equal("root", actor.Parent.Name);
            Assert.IsAssignableFrom<IRootActor>(actor.Parent);
            Assert.Equal(actor.Name, "calc");
        }

        [Fact]
        public void Actor_root_should_have_no_parent()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("calc");

            Assert.Null(actor.Parent.Parent);
        }
    }
    
}
