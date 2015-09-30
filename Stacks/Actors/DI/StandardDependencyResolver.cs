using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public class StandardDependencyResolver : IDependencyResolver
    {
        public T Resolve<T>(string resolverKey, IDictionary arguments)
        {
            var args = arguments.Values.Cast<object>().ToArray();

            if (args.Length == 0)
            {
                return Activator.CreateInstance<T>();
            }
            else
            {
                return (T)Activator.CreateInstance(typeof (T), args);
            }
            
        }

        public void Release(IActor actor)
        {
            // ignore
        }
    }
}
