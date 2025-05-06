using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Benchmarker
{
    public static class BenchmarkRunner
    {
        /* ========================================================================================== */
        // Usage: BenchmarkRunner.Benchmark(SomeMethod, label : "SomeMethod Benchmark");
        // Usage: BenchmarkRunner.Benchmark(SomeMethod, iterations : 10, label : "SomeMethod Benchmark with set iterations");
        public static BenchmarkResult Benchmark(Action action, int? iterations = null, string label = "Benchmark",
            bool verbose = false, bool removeOutliers = false, string? logFile = null)
        {
            if (iterations.HasValue && iterations <= 0) throw new ArgumentException("Iteration count must be positive.");

            var options = new BenchmarkOptions
            {
                Iterations = iterations,
                Label = label,
                Verbose = verbose,
                RemoveOutliers = removeOutliers,
                LogFile = logFile
            };

            int iterationsToRun = options.Iterations ?? DetermineOptimalIterations(action);
            return RunBenchmark(action, iterationsToRun, options);
        }

        /* ========================================================================================== */
        // Usage: BenchmarkRunner.Benchmark(arg1 => SomeMethod(arg1), arg1value, label : "SomeMethod Benchmark")
        // Usage: BenchmarkRunner.Benchmark(arg1 => SomeMethod(arg1), arg1value, iterations : 20, label : "SomeMethod Benchmark")
        public static BenchmarkResult Benchmark<T>(Action<T> action, T arg, int? iterations = null, string label = "Benchmark",
            bool verbose = false, bool removeOutliers = false, string? logFile = null)
        {
            if (iterations.HasValue && iterations <= 0) throw new ArgumentException("Iteration count must be positive.");

            var options = new BenchmarkOptions
            {
                Iterations = iterations,
                Label = label,
                Verbose = verbose,
                RemoveOutliers = removeOutliers,
                LogFile = logFile
            };

            int iterationsToRun = options.Iterations ?? DetermineOptimalIterations(() => action(arg));
            return RunBenchmark(() => action(arg), iterationsToRun, options);
        }

        /* ========================================================================================== */
        // Usage: BenchmarkRunner.Benchmark((arg1, arg2) => SomeMethod(arg1, arg2), arg1value, arg2value, label : "SomeMethod Benchmark")
        // Usage: BenchmarkRunner.Benchmark((arg1, arg2) => SomeMethod(arg1, arg2), arg1value, arg2value, iterations : 20, label : "SomeMethod Benchmark")
        public static BenchmarkResult Benchmark<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2, int? iterations = null, string label = "Benchmark",
            bool verbose = false, bool removeOutliers = false, string? logFile = null)
        {
            if (iterations.HasValue && iterations.Value <= 0) throw new ArgumentException("Iteration count must be positive.");

            var options = new BenchmarkOptions
            {
                Iterations = iterations,
                Label = label,
                Verbose = verbose,
                RemoveOutliers = removeOutliers,
                LogFile = logFile
            };

            int iterationsToRun = options.Iterations ?? DetermineOptimalIterations(() => action(arg1, arg2));
            return RunBenchmark(() => action(arg1, arg2), iterationsToRun, options);
        }

        /* ========================================================================================== */
        private static BenchmarkResult RunBenchmark(Action action, int iterationsToRun, BenchmarkOptions options)
        {
            var timings = new List<double>();
            Console.WriteLine($"{options.Label} - Running {iterationsToRun} iterations...\n");

            // Create log file writer if specified
            using StreamWriter? logWriter = options.LogFile != null ? new StreamWriter(options.LogFile, true) : null;

            logWriter?.WriteLine($"[{DateTime.Now}] {options.Label} - Running {iterationsToRun} iterations");

            for (int i = 0; i < iterationsToRun; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var stopwatch = Stopwatch.StartNew();
                action();
                stopwatch.Stop();

                double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                timings.Add(elapsedMs);
                if (options.Verbose)
                {
                    string message = "Iteration {i + 1}: {elapsedMs:F2} ms";
                    Console.WriteLine(message);
                    logWriter?.WriteLine(message);
                } 
            }

            // Remove outliers if requested
            if (options.RemoveOutliers && timings.Count > 3)
            {
                var sortedTimings = timings.OrderBy(t => t).ToList();
                int toRemove = Math.Max(1, sortedTimings.Count / 10);
                timings = sortedTimings.Skip(toRemove).Take(sortedTimings.Count - 2 * toRemove).ToList();

                logWriter?.WriteLine($"Removed {2 * toRemove} outliers, keepings {timings.Count} measurements");
            }

            var result = new BenchmarkResult(timings, options.Label);
            result.WriteToConsole();

            if (logWriter != null)
            {
                result.WriteToLog(logWriter);
            }
            
            return result;
        }

        /* ========================================================================================== */
        private static int DetermineOptimalIterations(Action testAction, int warmupIterations = 3)
        {
            // Warm-up to avoid JIT compilation time
            for (int i = 0; i < warmupIterations; i++)
            {
                testAction(); // Without timing
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopwatch = Stopwatch.StartNew();
            testAction();
            stopwatch.Stop();
            
            return CalculateIterations(stopwatch.Elapsed.TotalMilliseconds);
        }

        /* ========================================================================================== */
        private static int CalculateIterations(double elapsedMs)
        {
            // Determine iterations based on execution time
            if (elapsedMs <= 0.1) return 10000;            // Very fast: 0-0.1 ms
            if (elapsedMs <= 1) return 1000;               // Fast: 0.1-1 ms
            if (elapsedMs <= 10) return 100;               // Medium: 1-10 ms
            if (elapsedMs <= 100) return 50;               // Slow: 10-100 ms
            if (elapsedMs <= 1000) return 20;              // Very slow: 100ms-1s
            if (elapsedMs <= 10000) return 5;              // Extremely slow: 1s-10s
            return 1;                                       // Painfully slow: >10s
        }
    }

    /* ============================================================================================== */
    public class BenchmarkResult
    {
        public string Label { get; }
        public List<double> Timings { get; }
        public double Average => Timings.Average();
        public double Min => Timings.Min();
        public double Max => Timings.Max();
        public double StandardDeviation => Math.Sqrt(Timings.Average(v => Math.Pow(v - Average, 2)));
        public double Median => CalculatePercentile(Timings, 50);
        public double P95 => CalculatePercentile(Timings, 95);
        public double P99 => CalculatePercentile(Timings, 99);

        /* ========================================================================================== */
        public BenchmarkResult(List<double> timings, string label)
        {
            Timings = timings;
            Label = label;
        }

        /* ====================================================================================== */
        public void WriteToConsole()
        {
            Console.Write(GenerateSummary());
        }

        /* ====================================================================================== */
        public void WriteToLog(StreamWriter writer)
        {
            writer?.Write(GenerateSummary());
            writer?.WriteLine(); // Add a blank line for spacing
        }

        /* ========================================================================================== */
        private double CalculatePercentile(List<double> values, int percentile)
        {
            var sortedValues = new List<double>(values);
            sortedValues.Sort();
            int index = (int)Math.Ceiling((percentile / 100.0) * sortedValues.Count) -1;
            return sortedValues[Math.Max(0, index)];
        }

        /* ========================================================================================== */
        private string GenerateSummary()
        {
            string opener = $"\n------ {Label} Summary ------";
            string separator = string.Concat(Enumerable.Repeat("-", opener.Length - 1));

            var sb = new System.Text.StringBuilder();

            sb.AppendLine(opener);
            sb.AppendLine($"> Based on {Timings.Count} runs:");
            sb.AppendLine(separator);

            // Define column widths to ensure alignment
            const int metricColWidth = 14;  // Width of "Metric" column
            const int valueColWidth = 25;   // Width of "Value" column
            
            // Table headers
            string headerFormat = $"  {{0,-{metricColWidth}}}|  {{1,-{valueColWidth}}}";
            sb.AppendLine(string.Format(headerFormat, "Metric", "Value"));
            
            // Table separator line
            string tableSeparator = new string('-', metricColWidth + 2) + "+" + new string('-', valueColWidth + 2);
            sb.AppendLine(tableSeparator);
            
            // Row format
            string rowFormat = $"  {{0,-{metricColWidth}}}| {{1,-{valueColWidth}}}";
            
            // Table rows
            sb.AppendLine(string.Format(rowFormat, "Average", $"{Average:F3} ms (± {StandardDeviation:F3} ms)"));
            sb.AppendLine(string.Format(rowFormat, "Min", $"{Min:F3} ms"));
            sb.AppendLine(string.Format(rowFormat, "Max", $"{Max:F3} ms"));
            sb.AppendLine(string.Format(rowFormat, "Median (P50)", $"{Median:F3} ms"));
            sb.AppendLine(string.Format(rowFormat, "P95", $"{P95:F3} ms"));
            sb.AppendLine(string.Format(rowFormat, "P99", $"{P99:F3} ms"));
            
            sb.AppendLine(separator);
            
            return sb.ToString();
        }

    }

    /* ============================================================================================== */

    public class BenchmarkOptions
    {
        public int? Iterations { get; set; }
        public string Label { get; set; } = "Benchmark";
        public bool Verbose { get; set; } = false;
        public int WarmupIterations { get; set; } = 3;
        public bool RemoveOutliers { get; set; } = true;
        public string? LogFile { get; set; } = null;
    }
}