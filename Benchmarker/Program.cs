

using System.Reflection.Emit;

namespace Benchmarker
{
    public class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Benchmark(TestFunction, label: "TestFunction Benchmark", verbose: false, removeOutliers: true, logFile: "Logging_test.log");

            // using (var tracker = new PerformanceTracker("TestFunction Performance"))
            // {
            //     TestFunction();
            // }
        }

        static void TestFunction()
        {
            for (int i = 0; i < 200; i++)
            {
                double b = Math.Pow(i, 3);
            }
            
        }
    }
    
}
