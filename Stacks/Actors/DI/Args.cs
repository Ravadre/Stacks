using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public class Args : Dictionary<string, object>
    {
        public Args(params object[] args)
        {
            if (args == null)
                return;

            for (var i = 0; i < args.Length; ++i)
            {
                this[i.ToString()] = args[i];
            }
        }
    }
}
