using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Tests
{
    public static class ManualResetEventSlimAssertExtensions
    {
        public static void AssertWaitFor(this ManualResetEventSlim ev, int timeout)
        {
            if (!ev.Wait(timeout))
                throw new TimeoutException();
        }
    }
}
