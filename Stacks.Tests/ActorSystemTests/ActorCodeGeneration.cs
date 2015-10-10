using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
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

        [Fact]
        public void Actor_containing_non_task_return_type_should_be_wrapped()
        {
            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            Assert.Equal(15, actor.Test(10));
        }

        [Fact]
        public void Void_method_should_be_wrapped_properly()
        {
            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            actor.NoOp();
            Assert.Equal(10, actor.Test(4));
        }

        [Fact]
        public void Error_thrown_through_non_task_method_should_signal_exception_thrown()
        {
            var excThrown = new ManualResetEventSlim();

            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            var child = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>(parent: actor);

            actor.ExceptionThrown.Subscribe(s =>
            {
                excThrown.Set();
            });

            Assert.Equal(actor.Path, child.Parent.Path);

            Assert.Throws<Exception>(() =>
            {
                actor.Throw();
            });

            Assert.True(excThrown.IsSet);
        }

        [Fact]
        public void Error_thrown_through_non_observable_property_should_signal_exception_thrown()
        {
            var excThrown = new ManualResetEventSlim();

            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            var child = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>(parent: actor);

            actor.ExceptionThrown.Subscribe(s =>
            {
                excThrown.Set();
            });

            Assert.Equal(actor.Path, child.Parent.Path);

            Assert.Throws<Exception>(() =>
            {
                var x = actor.ThrowProp;
            });

            Assert.True(excThrown.IsSet);
        }

        [Fact]
        public void Actor_containing_properties_should_be_wrapped_properties()
        {
            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            Assert.Equal(15, actor.Test(10));
        }

        [Fact]
        public void Actor_get_and_set_properties_should_be_wrapped()
        {
            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            actor.Prop = 100;
            Assert.Equal(115, actor.Test(10));
        }

        [Fact]
        public void NoExceptionHandler_decorated_method_should_not_stop_actor_when_exn_is_thrown()
        {
            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            var child = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>(parent: actor);

            Assert.Throws<Exception>(() =>
            {
                actor.ThrowNoHandler();
            });

            Assert.Equal(actor.Path, child.Parent.Path);
            Assert.Equal(false, child.Stopped);
            Assert.Equal(false, actor.Stopped);
        }

        [Fact]
        public void NoExceptionHandler_decorated_property_should_not_stop_actor_when_exn_is_thrown()
        {
            var actor = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>();
            var child = ActorSystem.Default.CreateActor<ISyncActor, SyncActor>(parent: actor);

            Assert.Equal(actor.Path, child.Parent.Path);

            Assert.Throws<Exception>(() =>
            {
                var x = actor.ThrowNoHandlerProp;
            });

            Assert.Equal(actor.Path, child.Parent.Path);
            Assert.Equal(false, child.Stopped);
            Assert.Equal(false, actor.Stopped);
        }
    }

    public interface ISyncActor : IActor
    {
        
        int Test(int x);
        void NoOp();
        int Throw();
        [NoExceptionHandler]
        int ThrowNoHandler();

        int Prop { get; set; }
        int ThrowProp { get; }
        [NoExceptionHandler]
        int ThrowNoHandlerProp { get; }
        int SetOnly { set; }
    }

    public class SyncActor : Actor, ISyncActor
    {
        private int state = 0;
        public int Test(int x)
        {
            return x + 5 + state + Prop;
        }

        public void NoOp()
        {
            ++state;
        }

        public int Throw()
        {
            throw new Exception("test");
        }

        public int ThrowNoHandler()
        {
            throw new Exception("test");
        }

        protected override void OnStopped()
        {
        }

        public int Prop { get; set; }

        public int ThrowProp
        {
            get
            {
                throw new Exception("test");
            }
        }

        public int ThrowNoHandlerProp
        {
            get
            {
                throw new Exception("test");
            }
        }

        public int SetOnly
        {
            set
            {
                
            }
        }
    }

}
