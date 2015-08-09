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
    public class ActorTests
    {
        public ActorTests()
        {
            ActorSystem.Default.ResetSystem();
        }

        [Fact]
        public async Task When_actor_context_underlying_executor_is_stopped_it_should_throw_exception_when_it_is_awaited
            ()
        {
            var stoppedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(stoppedEvent), "ac");
            await actor.AddThenStop(5, 6);

            Assert.True(stoppedEvent.Wait(1000));

            await Assert.ThrowsAsync<ActorStoppedException>(async () =>
            {
                await actor.Div(6, 3);
            });
        }

        [Fact]
        public async Task OnStart_should_be_called_for_created_actor()
        {
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>("ac");

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

        [Fact]
        public async Task When_stopped_actor_should_receive_OnStopped_callback()
        {
            var stoppedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(stoppedEvent), "ac");
            var root = ActorSystem.Default.GetActor<IRootActor>("root");

            Assert.Equal(1, root.Childs.Count());
            var sum = await actor.AddThenStop(5, 6);

            Assert.True(stoppedEvent.Wait(1000));
            Assert.Equal(21, sum);
            Assert.Throws<Exception>(() =>
            {
                var ac = ActorSystem.Default.GetActor<ICalculatorActor>("ac");
            });
            Assert.Equal(0, root.Childs.Count());
        }
    }

    public class ThrowsOnStartActor : Actor, ICalculatorActor
    {
        protected override void OnStart()
        {
            throw new Exception("test");
        }

        public async Task<double> Div(double x, double y)
        {
            await Context;
            return 5;
        }
    }

    public class OnStartActor : Actor, ICalculatorExActor
    {
        private readonly ManualResetEventSlim stoppedEvent;
        private int offset = 0;

        public OnStartActor()
        {
            
        }

        public OnStartActor(ManualResetEventSlim stoppedEvent)
        {
            this.stoppedEvent = stoppedEvent;
        }

        public async Task<double> Add(double x, double y)
        {
            await Context;
            return x + y + offset;
        }

        protected override void OnStart()
        {
            offset = 10;
        }

        protected override void OnStopped()
        {
            stoppedEvent?.Set();
        }

        public async Task<double> Div(double x, double y)
        {
            await Context;

            return (x + offset) / y;
        }

        public async Task<double> AddThenStop(double x, double y)
        {
            await Context;

            Stop(true);
            return x + offset + y;
        }
    }
}
