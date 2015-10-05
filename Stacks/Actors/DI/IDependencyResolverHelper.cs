using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public interface IDependencyResolverHelper
    {
        T Resolve<T>(string name = null, IActor parent = null);
        T Resolve<T>(IDictionary<string, object> args, string name = null, IActor parent = null);

        T Resolve<T>(string resolveName, IDictionary<string, object> args = null, string name = null,
            IActor parent = null);
        void Release<T>(T obj);
    }
}
