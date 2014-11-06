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

        public static ActionBlockExecutorSettings Default
        {
            get { return new ActionBlockExecutorSettings(); }
        }

        public static ActionBlockExecutorSettings DefaultWith(bool supportSynchronizationContext = true)
        {
            var settings = new ActionBlockExecutorSettings();
            settings.SupportSynchronizationContext = supportSynchronizationContext;
            return settings;
        }
    }
}
