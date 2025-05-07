using System;
using System.Diagnostics;

namespace Enzyme
{
    public static class TraceConfig
    {
        static TraceConfig()
        {
            try
            {
                // Adds a console trace listener that outputs to the Debug Console
                Trace.Listeners.Add(new ConsoleTraceListener());

                // Configures auto-flush to ensure messages are written immediately
                Trace.AutoFlush = true;

                Trace.WriteLine($"Enzyme Trace Initialized at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                // If logging setup fails, attempts to output to console
                Console.WriteLine($"Failed to initialize trace logging: {ex.Message}");
            }
        }

        public static void EnsureInitialized()
        {
            // This method exists only to ensure the static constructor runs when called
            Trace.WriteLine("Trace system verified");
        }
    }
}
