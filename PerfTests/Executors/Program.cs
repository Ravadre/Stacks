using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks;

namespace Executors
{
    class Program
    {
        static void Main(string[] args)
        {
            RunSingleExecutor();

            Console.ReadLine();
        }

        static void RunSingleExecutor()
        {
            var test = new SingleExecutor(1000000, () => new ActionBlockExecutor());
            var stats = Benchmark.Measure(test.Run, 5);

            Console.WriteLine("Action block (ctx) executor: " + stats);
            Console.WriteLine("Action block (ctx) time per msg: " + stats.GetAverateTimePerActionNs(1000000).ToString() + "ns");

            test = new SingleExecutor(1000000, () => new ActionBlockExecutor(new ActionBlockExecutorSettings() { SupportSynchronizationContext = false }));
            stats = Benchmark.Measure(test.Run, 5);

            Console.WriteLine("Action block (no ctx) executor: " + stats);
            Console.WriteLine("Action block (no ctx) time per msg: " + stats.GetAverateTimePerActionNs(1000000).ToString() + "ns");
        }
    }
}
