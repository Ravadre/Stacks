using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class ConnectionTests
    {
        private IActorServerProxy server;
        private ITestActor client;
        private IActorClientProxy<ITestActor> clientProxy;

        [Fact]
        public void Client_should_be_able_to_connect_to_server()
        {
            Utils.CreateServerAndClient<ITestActor, TestActor>(out server, out client);
        }

        [Fact]
        public void Client_and_server_should_be_able_to_close_without_errors()
        {
            Utils.CreateServerAndClient<ITestActor, TestActor>(out server, out client);

            server.Stop();
            // ReSharper disable once SuspiciousTypeConversion.Global
            ((IActorClientProxy)client).Close();
        }

        [Fact]
        public void Client_should_return_task_which_fails_if_it_could_not_connect_to_server()
        {
            var clientTask = ActorClientProxy.CreateProxy<ITestActor>("tcp://localhost:" + Utils.FindFreePort());

            Assert.Throws(typeof(SocketException), () =>
                {
                    try
                    {
                        clientTask.Wait();
                    } catch (AggregateException exc)
                    {
                        throw exc.InnerException;
                    }
                });
        }

        [Fact]
        public void Client_should_signal_disconnection_when_server_is_closed()
        {
            var disconnected = new ManualResetEventSlim();
            Utils.CreateServerAndClientProxy<ITestActor, TestActor>(out server, out clientProxy);

            clientProxy.Disconnected.Subscribe(exn => { disconnected.Set(); });
            
            server.Stop();

            disconnected.AssertWaitFor();
        }
    }

    public interface ITestActor
    {
        Task Ping();
    }

    public class TestActor : Actor, ITestActor
    {
        public async Task Ping()
        {
            await Context;
        }

        public void Close()
        {
            throw new NotImplementedException();
        }
    }
}
