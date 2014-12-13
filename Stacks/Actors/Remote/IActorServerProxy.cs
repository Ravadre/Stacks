using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActorServerProxy
    {
        IPEndPoint BindEndPoint { get; }
        
        IObservable<IActorSession> ActorClientConnected { get; }
        IObservable<ClientActorDisconnectedData> ActorClientDisconnected { get; }
        
        Task<IActorSession[]> GetCurrentClientSessions();
        
        void Stop();
    }
}
