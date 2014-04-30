using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Stacks
{
    public class ActionContextExecutorSettings
    {
        public int QueueBoundedCapacity { get; set; }
        public int MaxDegreeOfParallelism { get; set; }

        public ActionContextExecutorSettings()
        {
            QueueBoundedCapacity = DataflowBlockOptions.Unbounded;
            MaxDegreeOfParallelism = 1;
        }

        private static ActionContextExecutorSettings @default = new ActionContextExecutorSettings();

        public static ActionContextExecutorSettings Default
        {
            get { return @default; }
        }


    }
}
