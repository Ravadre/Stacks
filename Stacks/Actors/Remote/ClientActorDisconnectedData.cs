using System;

namespace Stacks.Actors
{
    public class ClientActorDisconnectedData
    {
        public ClientActorDisconnectedData(IActorSession session, Exception error)
        {
            Session = session;
            Error = error;
        }

        public IActorSession Session { get; private set; }
        public Exception Error { get; private set; }
    }
}