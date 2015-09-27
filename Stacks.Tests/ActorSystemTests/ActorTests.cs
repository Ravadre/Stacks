using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
                ActorSystem.Default.CreateActor<ICalculatorActor, ThrowsOnStartActor>("ac");
            });
        }

        [Fact]
        public void If_OnStart_throws_actor_should_not_be_registered_as_a_child_to_root()
        {
            try
            {
                ActorSystem.Default.CreateActor<ICalculatorActor, ThrowsOnStartActor>("ac");
            }
            catch
            {}

            var root = ActorSystem.Default.GetActor<IRootActor>("root");
            Assert.Equal(0, root.Children.Count());
        }

        [Fact]
        public void If_OnStart_throws_actor_should_not_be_available_in_system()
        {
            try
            {
                ActorSystem.Default.CreateActor<ICalculatorActor, ThrowsOnStartActor>("ac");
            }
            catch
            { }

            Assert.Throws<Exception>(() =>
            {
                ActorSystem.Default.GetActor<ICalculatorActor>("ac");
            });

        }

        [Fact]
        public async Task When_stopped_actor_should_receive_OnStopped_callback()
        {
            var stoppedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(stoppedEvent), "ac");
            var root = ActorSystem.Default.GetActor<IRootActor>("root");

            Assert.Equal(1, root.Children.Count());
            var sum = await actor.AddThenStop(5, 6);

            Assert.True(stoppedEvent.Wait(1000));
            Assert.Equal(21, sum);
            Assert.Throws<Exception>(() =>
            {
                var ac = ActorSystem.Default.GetActor<ICalculatorActor>("ac");
            });
            Assert.Equal(0, root.Children.Count());
        }

        [Fact]
        public async Task If_actor_method_throws_actor_should_be_stopped()
        {
            var stoppedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(stoppedEvent), "ac");

            await Assert.ThrowsAsync<Exception>(async () =>
            {
                await actor.Throw("test error");
            });
            
            Assert.True(stoppedEvent.Wait(1000));
            Assert.Throws<Exception>(() =>
            {
                var ac = ActorSystem.Default.GetActor<ICalculatorActor>("ac");
            });
        }

        [Fact]
        public async Task When_actor_awaits_in_method_it_should_be_resumed_in_actor_context()
        {
            var stoppedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(stoppedEvent), "ac");

            var res = await actor.Complicated(2, 3);
            Assert.Equal(0, res);
        }

        [Fact]
        public async Task If_actor_method_throws_after_awaits_it_should_throw_in_context_and_be_caught()
        {
            var stoppedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(stoppedEvent), "ac");

            await Assert.ThrowsAsync<Exception>(async () =>
            {
                await actor.ComplicatedThenThrow(5, 4);
            });

            Assert.True(stoppedEvent.Wait(1000));
            Assert.Throws<Exception>(() =>
            {
                var ac = ActorSystem.Default.GetActor<ICalculatorActor>("ac");
            });
        }

        [Fact]
        public void If_actor_crashes_it_should_signal_Crashed_observable()
        {
            var crashedEvent = new ManualResetEventSlim();
            var actor = ActorSystem.Default.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(null),
                "ac");

            try
            {
                actor.Throw("test").Wait();
            }
            catch
            {
                // ignore
            }

            actor.Crashed.Subscribe(exn =>
            {
                crashedEvent.Set();
            });

            Assert.True(crashedEvent.Wait(100));
        }

        [Fact]
        public void Parent_should_be_able_to_restart_failing_child_actor()
        {
            var childCrashedEvent = new ManualResetEventSlim();

            var parent = ActorSystem.Default.CreateActor<IParentActor, ParentActor>(() => new ParentActor(childCrashedEvent));

            parent.CrashChild().Wait();
            Assert.True(childCrashedEvent.Wait(100));
            childCrashedEvent.Reset();
            parent.CrashChild().Wait();
            Assert.True(childCrashedEvent.Wait(100));
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

    public interface IParentActor
    {
        Task CrashChild();
    }

    public class ParentActor : Actor, IParentActor
    {
        private readonly ManualResetEventSlim childCrashed;

        public ParentActor(ManualResetEventSlim childCrashed)
        {
            this.childCrashed = childCrashed;
        }

        protected override void OnStart()
        {
            var child = System.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(), "child", this);

            child.Crashed.Subscribe(ChildCrashed);

            Assert.Equal(1, Children.Count());
        }

        private async void ChildCrashed(Exception exn)
        {
            await Context;

            childCrashed.Set();
            Assert.Equal(0, Children.Count());
            var child = System.CreateActor<ICalculatorExActor, OnStartActor>(() => new OnStartActor(), "child", this);
            child.Crashed.Subscribe(ChildCrashed);
            Assert.Equal(1, Children.Count());
        }

        public async Task CrashChild()
        {
            await Context;

            ((ICalculatorExActor) Children.First()).Throw("test");
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

            Stop().Wait();
            return x + offset + y;
        }

        public async Task<double> Throw(string msg)
        {
            await Context;
            throw new Exception(msg);
        }

        public async Task<double> Complicated(double x, double y)
        {
            await Context;

            var result = await Add(x, y) + x + y;

            await Task.Delay(50).ContinueWith(t => {});

            if (Executor.IsInContext())
                return 0;
            return result;
        }

        public async Task<double> ComplicatedThenThrow(double x, double y)
        {
            await Context;

            var result = await Add(x, y) + x + y;

            await Task.Delay(50).ContinueWith(t => { });

            if (Executor.IsInContext())
                throw new Exception("Is in context");
            return result;
        }


        public async Task NoOp()
        {
            await Context;
        }
    }
}
