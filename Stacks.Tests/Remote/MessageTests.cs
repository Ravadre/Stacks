using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class MessageTests
    {
        private IActorServerProxy server;
        private IMessageActor client;

        [Fact]
        public void Calling_method_should_call_it_on_server()
        {
            var impl = new MessageActor();

            Utils.CreateServerAndClient<MessageActor, IMessageActor>(impl, out server, out client);

            client.Ping().Wait();

            Assert.Equal(1, impl.PingsCalled);
        }
    }

    public interface IMessageActor : IActorClientProxy
    {
        Task Ping();
        Task<int> Random();
    }

    public class MessageActor : Actor, IMessageActor
    {
        private Random rng = new Random();

        public int PingsCalled { get; set; }

        public async Task Ping()
        {
            await Context;

            PingsCalled++;
        }

        public async Task<int> Random()
        {
            await Context;

            return rng.Next(100);
        }

        public void Close()
        {
            throw new NotImplementedException();
        }
    }
}
