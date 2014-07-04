using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stacks.Tests
{
    public class ExecutorTests
    {
        [Theory]
        [InlineData(typeof(ActionBlockExecutor))]
        [InlineData(typeof(BusyWaitExecutor))]
        public void PostTask_should_signal_task_when_action_is_completed(Type execType)
        {
            var exec = Activator.CreateInstance(execType) as IExecutor;

            int c = 0;

            var task = exec.PostTask(() =>
                {
                    Interlocked.Increment(ref c);
                });

            task.Wait();
            Assert.Equal(1, c);
        }

        [Theory]
        [InlineData(typeof(ActionBlockExecutor))]
        [InlineData(typeof(BusyWaitExecutor))]
        public void PostTask_should_signal_return_value_through_task(Type execType)
        {
            var exec = Activator.CreateInstance(execType) as IExecutor;

            var task = exec.PostTask(() =>
            {
                return 5;
            });

            Assert.Equal(5, task.Result);
        }

        [Theory]
        [InlineData(typeof(ActionBlockExecutor))]
        [InlineData(typeof(BusyWaitExecutor))]
        public void PostTask_should_rethrow_exception(Type execType)
        {
            var exec = Activator.CreateInstance(execType) as IExecutor;

            var task = exec.PostTask(() =>
            {
                throw new Exception("test");

            });

            Assert.Throws(typeof(AggregateException), () =>
                {
                    var r = task.Result;
                });
        }
    }
}
