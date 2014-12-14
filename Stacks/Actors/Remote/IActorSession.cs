using System.Runtime.Remoting.Messaging;

namespace Stacks.Actors
{
    public interface IActorSession
    {
        IFramedClient Client { get; }
        void Close();
    }

    public class ActorSession : IActorSession
    {
        public ActorSession(IFramedClient client)
        {
            Client = client;
        }

        public static IActorSession Current
        {
            get { return CallContext.LogicalGetData(ActorSessionCallContextKey) as IActorSession; }
        }

        internal static readonly string ActorSessionCallContextKey = "__stacks.actor.session0xc0de";

        public IFramedClient Client { get; private set; }

        public void Close()
        {
            Client.Close();
        }
    }
}