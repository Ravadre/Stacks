using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Stacks
{
    public class ActionBlockExecutorSettings
    {
        public int QueueBoundedCapacity { get; set; }
        public int MaxDegreeOfParallelism { get; set; }
        public bool SupportSynchronizationContext { get; set; }

        public ActionBlockExecutorSettings()
        {
            QueueBoundedCapacity = DataflowBlockOptions.Unbounded;
            MaxDegreeOfParallelism = 1;
            SupportSynchronizationContext = true;
        }

        private static ActionBlockExecutorSettings @default = new ActionBlockExecutorSettings();

        public static ActionBlockExecutorSettings Default
        {
            get { return @default; }
        }


    }
}
