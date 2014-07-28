using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class ConnectionTests
    {
        private IActorServerProxy server;
        private ITestActor client;

        [Fact]
        public void Client_should_be_able_to_connect_to_server()
        {
            Utils.CreateServerAndClient<TestActor, ITestActor>(out server, out client);
        }

        [Fact]
        public void Client_and_server_should_be_able_to_close_without_errors()
        {
            Utils.CreateServerAndClient<TestActor, ITestActor>(out server, out client);

            server.Stop();
            client.Close();
        }

        [Fact]
        public void Client_should_return_task_which_fails_if_it_could_not_connect_to_server()
        {
            var clientTask = ActorClientProxy.Create<ITestActor>("tcp://localhost:" + Utils.FindFreePort());

            Assert.Throws(typeof(SocketException), () =>
                {
                    try
                    {
                        var client = clientTask.Result;
                    } catch (AggregateException exc)
                    {
                        throw exc.InnerException;
                    }
                });
        }
    }

    public interface ITestActor : IActorClientProxy
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
