using System.Diagnostics;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

public sealed class ConsolePerformanceService : IPerformanceService
{
    public IDisposable StartTrace(string name)
    {
        return new ConsoleTrace(name);
    }

    private sealed class ConsoleTrace : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public ConsoleTrace(string name)
        {
            _name = name;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            Console.WriteLine($"[Performance] {_name}: {_stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
