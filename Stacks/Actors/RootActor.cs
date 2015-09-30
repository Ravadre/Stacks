using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class RootActor : Actor, IRootActor
    {
        public RootActor()
        {
            
        }
    }

    public interface IRootActor : IActor
    {
        
    }
}
