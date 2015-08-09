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
        public HierarchyTests()
        {
            ActorSystem.Default.ResetSystem();
        }

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

        [Fact]
        public void New_actors_that_derive_from_root_should_be_added_to_roots_childs()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("calc");
            var actor2 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("calc2");

            Assert.IsAssignableFrom<IRootActor>(actor.Parent);
            Assert.IsAssignableFrom<IRootActor>(actor2.Parent);
            Assert.Equal(2, actor.Parent.Childs.Count());
        }
    }
    
}
