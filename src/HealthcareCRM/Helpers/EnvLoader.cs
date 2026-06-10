using System;
using System.IO;

namespace HealthcareCRM.Helpers
{
    public static class EnvLoader
    {
        /// <summary>
        /// Loads environment variables from the specified .env file path into the current process.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the .env file.</param>
        public static void Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                // Fallback to searching in parent directories (useful during test runs)
                var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
                while (currentDir != null)
                {
                    var testPath = Path.Combine(currentDir.FullName, ".env");
                    if (File.Exists(testPath))
                    {
                        filePath = testPath;
                        break;
                    }
                    currentDir = currentDir.Parent;
                }
            }

            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Strip surrounding quotes
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
