using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActorSession
    {
        IFramedClient Client { get; }
        void Close();
    }

    public class ActorSession : IActorSession
    {
        public IFramedClient Client { get; private set; }

        public void Close()
        {
            Client.Close();
        }

        public ActorSession(IFramedClient client)
        {
            Client = client;
        }
    }
}
