using System;
using System.IO;
using System.Threading.Tasks;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for the MiBTestConfig constructor, the top level ReadConfigFileAsync flow and the
    /// [DLL] section (GetDLLConfig).
    /// </summary>
    [TestFixture]
    public class MiBTestConfigDllSectionTests : MiBTestConfigTestBase
    {
        private const string Dll = MiBTestConfigFixture.Dll;

        #region Constructor

        [TestCase(null)]
        [TestCase("")]
        public void Constructor_BlankPath_Throws(string path)
        {
            var ex = Assert.Throws<ApplicationException>(() => new MiBTestConfig(path, "1.0"));
            Assert.That(ex.Message, Does.Contain("blank"));
        }

        [Test]
        public void Constructor_FileNotFound_Throws()
        {
            string missing = TempDir.PathOf("missing.ini");

            var ex = Assert.Throws<ApplicationException>(() => new MiBTestConfig(missing, "1.0"));
            Assert.That(ex.Message, Does.Contain("not found"));
        }

        [Test]
        public void Constructor_ExistingFile_StoresPathAndParentVersion()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            Assert.That(cfg.ConfigFilePath, Is.EqualTo(TempDir.PathOf("MiBTest.ini")));
            Assert.That(cfg.ParentFileVersion, Is.EqualTo("1.2.3.4"));
        }

        #endregion

        #region ReadConfigFileAsync

        [Test]
        public async Task ReadConfigFileAsync_ValidSocketCommConfig_ReturnsSuccess()
        {
            var (_, rc, res) = await LoadAsync(MiBTestConfigFixture.ValidSocketCommConfig());

            Assert.That(rc, Is.EqualTo(1));
            Assert.That(res, Is.Empty);
        }

        [Test]
        public async Task ReadConfigFileAsync_FileDeletedAfterConstruction_ReturnsReadError()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());
            File.Delete(cfg.ConfigFilePath);

            var (rc, res) = await cfg.ReadConfigFileAsync();

            Assert.That(rc, Is.EqualTo(-1));
            Assert.That(res, Does.Contain("Failed to read configuration file"));
        }

        [Test]
        public async Task ReadConfigFileAsync_MalformedIni_ReturnsReadError()
        {
            string path = TempDir.WriteFile("MiBTest.ini", "[DLL]\nthis line is not a key value pair\n");
            var cfg = new MiBTestConfig(path, "1.0");

            var (rc, res) = await cfg.ReadConfigFileAsync();

            Assert.That(rc, Is.EqualTo(-1));
            Assert.That(res, Does.Contain("Failed to read configuration file"));
        }

        [Test]
        public void GetDLLConfig_BeforeFileIsRead_ReturnsExceptionError()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            var (rc, res) = cfg.GetDLLConfig();

            Assert.That(rc, Is.EqualTo(-1));
            Assert.That(res, Does.Contain("exception"));
        }

        #endregion

        #region DLL section values

        [Test]
        public async Task DllSection_ValidValues_AreExtractedAndTrimmed()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.HandlerCommType, Is.EqualTo(HandlerCommTypes.SocketComm));
            Assert.That(cfg.SocCommHandlerDllFilePath, Is.EqualTo(@"C:\Handlers\SocComm.dll"));
            Assert.That(cfg.SocCommHandlerClassName, Is.EqualTo("SocComm.Handler"));
            Assert.That(cfg.MiBDllFilePath, Is.EqualTo(@"C:\MiB\MiB.dll"));
            Assert.That(cfg.MiBClassName, Is.EqualTo("MiB.Main"));
            Assert.That(cfg.PPDirPath, Is.EqualTo(@"C:\PP"));
            Assert.That(cfg.DSDirPath, Is.EqualTo(@"C:\DS"));
            Assert.That(cfg.BinaryRecipeFormat, Is.EqualTo(0));
            Assert.That(cfg.StiGemCfgFilePath, Is.EqualTo(@"C:\StiGem\Custom.cfg"));
        }

        [Test]
        public async Task DllSection_Missing_ReturnsMinus2()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveSection(Dll);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain("DLL section not found"));
        }

        [Test]
        public async Task DllSection_NoKeys_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().ClearSection(Dll);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("does not contain any keys"));
        }

        [TestCase("SocketCommHandlerDLLFilePath", -4)]
        [TestCase("SocketCommHandlerClassName", -5)]
        [TestCase("MiBDllFilePath", -6)]
        [TestCase("MiBClassName", -7)]
        [TestCase("PPDirPath", -8)]
        [TestCase("DSDirPath", -8)]
        public async Task DllSection_MissingMandatoryKey_ReturnsErrorCode(string key, int expectedRc)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveKey(Dll, key);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(expectedRc));
            Assert.That(res, Does.Contain(key));
        }

        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("5", 1)]   // any non-zero number means binary
        [TestCase("-1", 1)]
        public async Task BinaryRecipeFormat_NumericValue_IsNormalisedToZeroOrOne(string value, int expected)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(Dll, "BinaryRecipeFormat", value);

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.BinaryRecipeFormat, Is.EqualTo(expected));
        }

        [Test]
        public async Task BinaryRecipeFormat_Missing_DefaultsToBinaryWithWarning()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveKey(Dll, "BinaryRecipeFormat");
            var (cfg, rc, _) = await LoadAsync(ini);
            Assert.That(rc, Is.EqualTo(1));

            // ReadConfigFileAsync drops warnings, so call GetDLLConfig directly to see it.
            var (dllRc, dllRes) = cfg.GetDLLConfig();

            Assert.That(dllRc, Is.EqualTo(1));
            Assert.That(dllRes, Does.StartWith("WARNING").And.Contain("BinaryRecipeFormat"));
            Assert.That(cfg.BinaryRecipeFormat, Is.EqualTo(1));
        }

        [Test]
        public async Task BinaryRecipeFormat_NonNumeric_DefaultsToBinaryWithWarning()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(Dll, "BinaryRecipeFormat", "abc");
            var (cfg, rc, _) = await LoadAsync(ini);
            Assert.That(rc, Is.EqualTo(1));

            var (dllRc, dllRes) = cfg.GetDLLConfig();

            Assert.That(dllRc, Is.EqualTo(1));
            Assert.That(dllRes, Does.Contain("not numeric"));
            Assert.That(cfg.BinaryRecipeFormat, Is.EqualTo(1));
        }

        [Test]
        public async Task StiGemConfigFilePath_Missing_UsesDefaultPathWithWarning()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveKey(Dll, "StiGemConfigFilePath");
            var (cfg, rc, _) = await LoadAsync(ini);
            Assert.That(rc, Is.EqualTo(1));

            var (dllRc, dllRes) = cfg.GetDLLConfig();

            Assert.That(dllRc, Is.EqualTo(1));
            Assert.That(dllRes, Does.Contain("StiGemConfigFilePath"));
            Assert.That(cfg.StiGemCfgFilePath, Is.EqualTo(@"C:\StiGem\StiGemMsg.cfg"));
        }

        [TestCase("0")]
        [TestCase("7")]   // undefined enum value
        [TestCase("abc")] // non-numeric
        public async Task CommType_ZeroInvalidOrNonNumeric_FallsBackToSocketComm(string value)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(Dll, "CommType", value);

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.HandlerCommType, Is.EqualTo(HandlerCommTypes.SocketComm));
        }

        [Test]
        public async Task CommType_StiCli_RequiresStiCliKeysInsteadOfSocketKeys()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig()
                .Set(Dll, "CommType", "1")
                .RemoveKey(Dll, "SocketCommHandlerDLLFilePath")
                .RemoveKey(Dll, "SocketCommHandlerClassName");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("STICLICommHandlerDLLFilePath"));
        }

        [Test]
        public async Task CommType_StiCli_MissingClassName_ReturnsMinus5()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig()
                .Set(Dll, "CommType", "1")
                .Set(Dll, "STICLICommHandlerDLLFilePath", @"C:\Cli\Cli.dll");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-5));
            Assert.That(res, Does.Contain("STICLICommHandlerClassName"));
        }

        [Test]
        public async Task CommType_StiCli_SkipsSocketSectionsAndReadsGemCliConfig()
        {
            // All socket-only sections removed; the STI CLI path must not look for them and
            // instead fails on the missing [STICLI SECSGEM] section (GemCliConfig rc -3).
            var ini = new IniBuilder()
                .Set(Dll, "CommType", "1")
                .Set(Dll, "STICLICommHandlerDLLFilePath", @" C:\Cli\Cli.dll ")
                .Set(Dll, "STICLICommHandlerClassName", "Cli.Handler")
                .Set(Dll, "MiBDllFilePath", @"C:\MiB\MiB.dll")
                .Set(Dll, "MiBClassName", "MiB.Main")
                .Set(Dll, "PPDirPath", @"C:\PP")
                .Set(Dll, "DSDirPath", @"C:\DS")
                .Set("LinkStateEnum", "Enabled", "1")
                .Set("ControlStateEnum", "OnlineRemote", "5")
                .Set("SpoolStateEnum", "Active", "1");

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("STICLI SECSGEM"));
            Assert.That(cfg.HandlerCommType, Is.EqualTo(HandlerCommTypes.STICLIComm));
            Assert.That(cfg.STICLICommHandlerDLLPath, Is.EqualTo(@"C:\Cli\Cli.dll"));
            Assert.That(cfg.STICLICommHandlerClassName, Is.EqualTo("Cli.Handler"));
            Assert.That(cfg.gemCliConfig, Is.Not.Null);
        }

        #endregion

        #region ToString

        [Test]
        public async Task ToString_AfterSuccessfulRead_ContainsKeySettings()
        {
            var cfg = await LoadValidAsync();

            string text = cfg.ToString();

            Assert.That(text, Does.Contain("Handler Comm Type=SocketComm"));
            Assert.That(text, Does.Contain(@"C:\Handlers\SocComm.dll"));
            Assert.That(text, Does.Contain("Number of handling function settings found: 3"));
            Assert.That(text, Does.Contain("Handling method name=LotStartCmd"));
        }

        [Test]
        public void ToString_BeforeRead_DoesNotThrow()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            Assert.That(() => cfg.ToString(), Throws.Nothing);
            Assert.That(cfg.ToString(), Does.Contain("No message handling function settings found"));
        }

        #endregion
    }
}
