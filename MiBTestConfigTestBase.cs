using System.Threading.Tasks;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Common set-up for <see cref="MiBTestConfig"/> tests. The parsed INI data is private
    /// to MiBTestConfig, so every test goes through a real file and ReadConfigFileAsync,
    /// exactly as the MiBTest application does.
    /// </summary>
    public abstract class MiBTestConfigTestBase
    {
        protected TempDirectory TempDir;

        [SetUp]
        public void CreateTempDir() => TempDir = new TempDirectory();

        [TearDown]
        public void DeleteTempDir() => TempDir.Dispose();

        protected MiBTestConfig CreateConfig(IniBuilder ini)
        {
            string path = TempDir.WriteFile("MiBTest.ini", ini.ToString());
            return new MiBTestConfig(path, "1.2.3.4");
        }

        /// <summary>
        /// Writes the INI, reads it and returns the config together with the read result.
        /// </summary>
        protected async Task<(MiBTestConfig cfg, int rc, string res)> LoadAsync(IniBuilder ini)
        {
            var cfg = CreateConfig(ini);
            var (rc, res) = await cfg.ReadConfigFileAsync();
            return (cfg, rc, res);
        }

        /// <summary>
        /// Loads the default valid config and asserts it read successfully.
        /// </summary>
        protected async Task<MiBTestConfig> LoadValidAsync()
        {
            var (cfg, rc, res) = await LoadAsync(MiBTestConfigFixture.ValidSocketCommConfig());
            Assert.That(rc, Is.EqualTo(1), res);
            return cfg;
        }
    }
}
