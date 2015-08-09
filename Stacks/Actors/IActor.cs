using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActor
    {
        string Name { get; }
        IActor Parent { get; }
        IEnumerable<IActor> Childs { get; }
    }
}
