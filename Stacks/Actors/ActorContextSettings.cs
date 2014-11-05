using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class ActorContextSettings
    {
        public bool SupportSynchronizationContext { get; set; }

        public ActorContextSettings()
        {
            SupportSynchronizationContext = true;
        }

        public static ActorContextSettings Default
        {
            get { return new ActorContextSettings(); }
        }
    }
}
