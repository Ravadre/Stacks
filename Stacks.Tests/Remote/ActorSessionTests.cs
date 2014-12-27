using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class ActorSessionTests
    {
        private IActorServerProxy server;
        private IMessageActor client;


        [Fact]
        public async void If_Enabled_IActorSession_should_get_accessible_from_ActorSession_Current()
        {
            var opts = new ActorServerProxyOptions(actorSessionInjectionEnabled: true);
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(opts, out server, out client);

            var client2 = ActorClientProxy.CreateActor<IMessageActor>("tcp://localhost:" + server.BindEndPoint.Port).Result;

            await client.PassDataForContext(1);
            await client.PassDataForContext(1);
            await client2.PassDataForContext(2);
            await client.PassDataForContext(1);
            await client2.PassDataForContext(2);

        }

        [Fact]
        public void If_Enabled_StressTest_IActorSession_should_get_accessible_from_ActorSession_Current()
        {
            var opts = new ActorServerProxyOptions(actorSessionInjectionEnabled: true);
            IMessageActor[] clients = new IMessageActor[20];

            Utils.CreateServerAndClient<MessageActor, IMessageActor>(opts, out server, out clients[0]);

            for (int i = 1; i < 20; ++i)
                clients[i] = ActorClientProxy.CreateActor<IMessageActor>("tcp://localhost:" + server.BindEndPoint.Port).Result;

            var tasks = new List<Task>();

            for (int i = 0; i < 100; ++i)
            {
                int idx = i % 20;
                tasks.Add(clients[idx].StressTestSession(idx));
            }

            Task.WaitAll(tasks.ToArray());
        }

        [Fact]
        public async void If_Not_Enabled_ActorSession_Current_should_be_null()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            await client.AssertActorSessionIsNull();
        }
    }
}
