using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public static class Executor
    {
        public static bool IsInContext()
        {
            var ctx = SynchronizationContext.Current;

            if (ctx is IExecutor)
                return true;

            return false;
        }

        public static string GetCurrentFullName()
        {
            var ctx = SynchronizationContext.Current;

            if (ctx is IExecutor)
                return ctx.ToString();

            return string.Empty;
        }

        public static string GetCurrentName()
        {
            var ctx = SynchronizationContext.Current;

            var executor = ctx as IExecutor;
            if (executor != null)
                return executor.Name;

            return string.Empty;
        }
    }
}
