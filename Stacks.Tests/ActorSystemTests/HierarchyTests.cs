using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            Assert.Equal(2, actor.Parent.Children.Count());
        }

        [Fact]
        public void Having_actor_as_child_of_other_actor_should_set_hierarchy_properly()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("calc");
            var actor2 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("calc2", actor);

            Assert.IsAssignableFrom<IRootActor>(actor.Parent);
            Assert.Equal(1, actor.Parent.Children.Count());

            Assert.IsAssignableFrom<IRootActor>(actor2.Parent.Parent);
            Assert.Equal(1, actor2.Parent.Children.Count());
            Assert.Equal(actor2, actor2.Parent.Children.First());

        }

        [Fact]
        public async Task When_actor_is_stopped_it_should_stop_all_its_children()
        {
            var a1 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a1");
            var a2 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a2");
            var a3 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a3");

            var a11 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a11", a1);
            var a12 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a12", a1);
            var a21 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a21", a2);
            var a22 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a22", a2);
            var a31 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a31", a3);
            var a32 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a32", a3);

            var a121 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("a121", a12);

            var root = a1.Parent;

            await a1.Stop();
            
            Assert.Equal(2, root.Children.Count());
            Assert.Null(ActorSystem.Default.TryGetActor<ICalculatorActor>("a1"));
            Assert.Null(ActorSystem.Default.TryGetActor<ICalculatorActor>("a11"));
            Assert.Null(ActorSystem.Default.TryGetActor<ICalculatorActor>("a12"));
            Assert.Null(ActorSystem.Default.TryGetActor<ICalculatorActor>("a121"));
            Assert.NotNull(ActorSystem.Default.TryGetActor<ICalculatorActor>("a2"));
            Assert.NotNull(ActorSystem.Default.TryGetActor<ICalculatorActor>("a2/a21"));
            Assert.NotNull(ActorSystem.Default.TryGetActor<ICalculatorActor>("a2/a22"));
            Assert.NotNull(ActorSystem.Default.TryGetActor<ICalculatorActor>("a3"));
            Assert.NotNull(ActorSystem.Default.TryGetActor<ICalculatorActor>("a3/a31"));
            Assert.NotNull(ActorSystem.Default.TryGetActor<ICalculatorActor>("a3/a32"));
        }

        [Fact]
        public async Task When_actor_is_stopping_it_should_not_be_able_to_add_children_to_it()
        {
            var a1 = ActorSystem.Default.CreateActor<ICalculatorActor, LongStopActor>("a1");

            var stopping = a1.Stop();

            Thread.Sleep(50);
            Assert.ThrowsAny<Exception>(() =>
            {
                ActorSystem.Default.CreateActor<ICalculatorActor, LongStopActor>("a11", a1);
            });

            await stopping;

            Assert.Equal(0, a1.Children.Count());
            Assert.Null(ActorSystem.Default.TryGetActor<ICalculatorActor>("a11"));

        }

        [Fact]
        public void Creating_actor_with_same_name_and_parent_should_fail()
        {
            var parent = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("p");

            var child1 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("c", parent);

            Assert.ThrowsAny<Exception>(() =>
            {
                var child2 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("c", parent);
            });
        }

        [Fact]
        public void Creating_actor_with_same_name_but_different_path_should_succeed()
        {
            var parent = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("p");

            var child1 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("c", parent);
            var child2 = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("c", child1);
        }

        [Fact]
        public void Creating_named_actor_as_child_of_anonymous_one_should_succeed()
        {
            var parent = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>();
            var child = ActorSystem.Default.CreateActor<ICalculatorActor, CalculatorActor>("c", parent);
            Assert.Equal("/root/$b/c/", child.Path);
        }
    }
    
}
