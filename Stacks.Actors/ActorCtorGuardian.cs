using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    internal class ActorCtorGuardian
    {
        [ThreadStatic]
        private static ActorCtorGuardian current;

        public static void SetGuard()
        {
            current = new ActorCtorGuardian();
        }

        public static bool IsGuarded()
        {
            return current != null;
        }

        public static void ClearGuard()
        {
            current = null;
        }
    }
}
