using System.Diagnostics;

namespace Benchmarker
{
    // Usage: using (var tracker = new PerformanceTracker("TestFunction Performance")) { ... }
    public class PerformanceTracker : IDisposable
    {
        private Stopwatch _stopwatch;
        private long _startMemory;
        private long _endMemory;

        public string Label { get; }

        /* ====================================================================================== */
        public PerformanceTracker(string label = "Performance Tracker")
        {
            Label = label;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _startMemory = GC.GetTotalMemory(forceFullCollection: false);
            _stopwatch = Stopwatch.StartNew();

            Console.WriteLine($"{Label} started...\n");
        }

        /* ====================================================================================== */
        public void Dispose()
        {
            _stopwatch.Stop();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _endMemory = GC.GetTotalMemory(forceFullCollection: false);

            string opener = $"\n--- {Label} Summary ---";
            Console.WriteLine(opener);
            Console.WriteLine($"Elapsed time    : {_stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Memory start    : {FormatBytes(_startMemory)}");
            Console.WriteLine($"Memory end      : {FormatBytes(_endMemory)}");
            Console.WriteLine($"Memory delta    : {FormatBytes(_endMemory - _startMemory)}");
            Console.WriteLine(string.Concat(Enumerable.Repeat("-", opener.Length - 1)));
        }

        /* ====================================================================================== */
        private static string FormatBytes(long bytes)
        {
            string[] sizes = {"B", "KB" ,"MB" , "GB"};
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
