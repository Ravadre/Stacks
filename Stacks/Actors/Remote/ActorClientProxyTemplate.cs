using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public class ActorClientProxyTemplate
    {
        private IPEndPoint endPoint;
        private ActorRemoteMessageClient client;

        public ActorClientProxyTemplate(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;
            client = new ActorRemoteMessageClient(
                        new FramedClient(
                            new SocketClient()));

            client.MessageReceived += MessageReceived;

            client.Connect(endPoint);
        }

        void MessageReceived(string msgName, long requestId, MemoryStream ms)
        {
        }

    }
}
