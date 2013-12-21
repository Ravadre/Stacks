using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Stacks.Executors
{
    public class ActorContextSettings
    {
        public int QueueBoundedCapacity { get; set; }
        public int MaxDegreeOfParallelism { get; set; }

        public ActorContextSettings()
        {
            QueueBoundedCapacity = DataflowBlockOptions.Unbounded;
            MaxDegreeOfParallelism = 1;
        }

        private static ActorContextSettings @default = new ActorContextSettings();

        public static ActorContextSettings Default
        {
            get { return @default; }
        }


    }
}
