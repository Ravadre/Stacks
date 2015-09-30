using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public interface IDependencyResolver
    {
        object Resolve<T>(Type interfaceType, string resolverKey, IDictionary arguments);
        void Release(IActor actor);

    }
}
