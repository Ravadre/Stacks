using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class ActorServerProxyOptions
    {
        public bool ActorSessionInjectionEnabled { get; private set; }

        public ActorServerProxyOptions(bool actorSessionInjectionEnabled)
        {
            ActorSessionInjectionEnabled = actorSessionInjectionEnabled;
        }

        public static readonly ActorServerProxyOptions Default = 
            new ActorServerProxyOptions(actorSessionInjectionEnabled: false);
    }
}
