using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class ActorWrapperBase : IActor
    {
        private readonly IActor actorImplementation;
        public string Name => actorImplementation.Name;

        public ActorWrapperBase(IActor actorImplementation)
        {
            this.actorImplementation = actorImplementation;
        }
    }
}
