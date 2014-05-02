using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks;

namespace Executors
{
    class SingleExecutor
    {
        private int messageCount;
        private Func<IExecutor> execFactory;

        public SingleExecutor(int messageCount, Func<IExecutor> execFactory)
        {
            this.messageCount = messageCount;
            this.execFactory = execFactory;
        }

        public void Run()
        {
            var ex1 = execFactory();

            var counter = 0L;

            for (int i = 0; i < messageCount; ++i)
                ex1.Enqueue(() => ++counter);

            ex1.Stop().Wait();

            if (counter != messageCount)
                throw new Exception("Invalid result");
        }
    }
}
