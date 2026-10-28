using System;
using System.IO;

namespace ConfigurationManager.Tests.Helpers
{
    /// <summary>
    /// Creates a unique scratch folder under %TEMP% for a test and deletes it on Dispose,
    /// so config files written by one test can never leak into another.
    /// </summary>
    public sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ConfigurationManager.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Writes a file into the scratch folder and returns its full path.
        /// </summary>
        public string WriteFile(string fileName, string contents)
        {
            string fullPath = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        /// <summary>
        /// Returns the full path of a file in the scratch folder without creating it.
        /// </summary>
        public string PathOf(string fileName) => System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
            catch (IOException)
            {
                // Best effort clean-up; a locked file must not fail the test run.
            }
        }
    }
}
