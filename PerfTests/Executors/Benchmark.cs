using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Executors
{
    public class Benchmark
    {
        public static BenchmarkStats Measure(Action action, int timesToRepeat)
        {
            var times = new List<TimeSpan>();
            var sw = new Stopwatch();

            for (int i = 0; i < timesToRepeat; ++i)
            {
                GC.Collect(); GC.Collect();

                sw.Restart();
                action();
                sw.Stop();

                if (i > 0)
                    times.Add(sw.Elapsed);
            }

            return new BenchmarkStats(times);
        }
    }

    public class BenchmarkStats
    {
        public IReadOnlyList<TimeSpan> Times { get; private set; }
        public TimeSpan TotalAverageTime { get; private set; }

        public BenchmarkStats(IEnumerable<TimeSpan> times)
        {
            Times = times.ToList();
            TotalAverageTime = TimeSpan.FromSeconds(Times.Average(t => t.TotalSeconds));
        }

        public long GetAverateTimePerActionNs(int actionCount)
        {
            return TotalAverageTime.Ticks * 1000000L / TimeSpan.TicksPerMillisecond / actionCount;
        }

        public override string ToString()
        {
            return TotalAverageTime.TotalSeconds.ToString("F4") + "s";
        }
    }
}
