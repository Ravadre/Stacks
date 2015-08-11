using System;
using System.Threading;
using Xunit;

namespace Stacks.Tests
{
    public class ExecutorTests
    {
        [Theory]
        [InlineData(typeof (ActionBlockExecutor))]
        [InlineData(typeof (BusyWaitExecutor))]
        public void PostTask_should_signal_task_when_action_is_completed(Type execType)
        {
            var exec = Activator.CreateInstance(execType) as IExecutor;

            var c = 0;

            var task = exec.PostTask(() => { Interlocked.Increment(ref c); });

            task.Wait();
            Assert.Equal(1, c);
        }

        [Theory]
        [InlineData(typeof (ActionBlockExecutor))]
        [InlineData(typeof (BusyWaitExecutor))]
        public void PostTask_should_signal_return_value_through_task(Type execType)
        {
            var exec = Activator.CreateInstance(execType) as IExecutor;

            var task = exec.PostTask(() => { return 5; });

            Assert.Equal(5, task.Result);
        }

        [Theory]
        [InlineData(typeof (ActionBlockExecutor))]
        [InlineData(typeof (BusyWaitExecutor))]
        public void PostTask_should_rethrow_exception(Type execType)
        {
            var exec = Activator.CreateInstance(execType) as IExecutor;

            var task = exec.PostTask(() => { throw new Exception("test"); });

            Assert.Throws(typeof (AggregateException), () => { var r = task.Result; });
        }

        [Theory]
        [InlineData(typeof (ActionBlockExecutor))]
        [InlineData(typeof (BusyWaitExecutor))]
        public void Executor_should_not_be_stopped_after_posttask_throws_exception(Type execType)
        {
            var errorOccured = new ManualResetEventSlim();
            var execIsRunning = new ManualResetEventSlim();

            var exec = Activator.CreateInstance(execType) as IExecutor;

            exec.Error += exception => { errorOccured.Set(); };

            var task = exec.PostTask(() => { throw new Exception("test"); });

            Assert.Throws(typeof (AggregateException), () => { task.Wait(); });


            exec.PostTask(() => { execIsRunning.Set(); }).Wait();

            Assert.False(errorOccured.IsSet);
            Assert.True(execIsRunning.IsSet);
        }
    }
}