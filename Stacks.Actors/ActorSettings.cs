using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class ActorSettings
    {
        public bool SupportSynchronizationContext { get; set; }

        public ActorSettings()
        {
            SupportSynchronizationContext = true;
        }

        public static ActorSettings Default
        {
            get { return new ActorSettings(); }
        }
    }
}
