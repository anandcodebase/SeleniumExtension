using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public static class SelectorPersistence
    {
        private static readonly object _lock = new();

        public static string OutputDirectory { get; set; } =
            Path.Combine(AppContext.BaseDirectory, "selector-history");

        public static void Flush(IEnumerable<object> telemetrySnapshot)
        {
            Directory.CreateDirectory(OutputDirectory);

            var file = Path.Combine(
                OutputDirectory,
                $"selectors-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");

            var json = JsonSerializer.Serialize(
                telemetrySnapshot,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            lock (_lock)
            {
                File.WriteAllText(file, json);
            }
        }
    }
}
