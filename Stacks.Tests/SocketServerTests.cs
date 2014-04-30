using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stacks.Tests
{
    public class SocketServerTests
    {
       
        public class Starting_and_stopping : SocketServerTests
        {
            [Fact]
            public void Starting_two_times_should_throw()
            {
                var server = ServerHelpers.CreateServer();
                server.Start();

                Assert.Throws(typeof(InvalidOperationException), () =>
                    {
                        server.Start();
                    });
            }

            [Fact]
            public void Starting_should_call_started_callback()
            {
                var started = new ManualResetEventSlim();
                var server = ServerHelpers.CreateServer();

                server.Started += () => { started.Set(); };
                server.Start();

                started.AssertWaitFor(2000);

                server.StopAndAssertStopped();
            }

            [Fact]
            public void Starting_and_stopping_should_call_both_callbacks()
            {
                var started = new ManualResetEventSlim();
                var stopped = new ManualResetEventSlim();
                var server = ServerHelpers.CreateServer();

                server.Started += () => { started.Set(); };
                server.Stopped += () => { stopped.Set(); };

                server.Start();
                server.Stop();

                started.AssertWaitFor(2000);
                stopped.AssertWaitFor(2000);
            }

            [Fact]
            public void When_exception_is_thrown_inside_callback_executor_should_signal_error()
            {
                var errOccured = new ManualResetEventSlim();

                var exec = new ActionBlockExecutor(null, new ActionContextExecutorSettings());
                var server = ServerHelpers.CreateServer(exec);

                exec.Error += exc => { Assert.Equal("abcdef", exc.Message); errOccured.Set(); };
                server.Started += () => { throw new Exception("abcdef"); };
                
                server.Start();

                errOccured.AssertWaitFor(2000);

                server.Stop();
                
            }
        }
    }
}
