using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    static class ExecutorHelper
    {
        internal static Task<System.Reactive.Unit> PostTask(IExecutor exec, Action action)
        {
            //Not reusing <T> implementation, so additional lambda allocation can be ommited
            var tcs = new TaskCompletionSource<System.Reactive.Unit>();

            exec.Enqueue(() =>
                {
                    try
                    {
                        if (action != null)
                            action();
                        tcs.SetResult(System.Reactive.Unit.Default);
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                });

            return tcs.Task;
        }

        internal static Task<T> PostTask<T>(IExecutor exec, Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();

            exec.Enqueue(() =>
                {
                    try
                    {
                        tcs.SetResult(func());
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                });

            return tcs.Task;
        }
    }
}
