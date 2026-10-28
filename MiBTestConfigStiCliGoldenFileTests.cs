using System.IO;
using System.Threading.Tasks;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Golden-file tests for STI CLI mode: a frozen copy of a real STI CLI MiBTest.ini and the
    /// files it points to (TestData2). The real file uses absolute D:\tScanSecsGem\MiBTest\ paths,
    /// so each test copies the set into a temp folder and rewrites that prefix.
    /// </summary>
    [TestFixture]
    public class MiBTestConfigStiCliGoldenFileTests
    {
        private const string RealFolder = @"D:\tScanSecsGem\MiBTest\";
        private TempDirectory tempDir;

        [SetUp] public void SetUp() => tempDir = new TempDirectory();
        [TearDown] public void TearDown() => tempDir.Dispose();

        private static string Source(string file) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData2", file);

        /// Copies the real file set into the temp folder and points MiBTest.ini at it.
        private string PrepareGoldenSet()
        {
            File.Copy(Source("CliGemVar 1.json"), tempDir.PathOf("CliGemVar 1.json"));
            File.Copy(Source("GetAttrReq.dat"), tempDir.PathOf("GetAttrReq.dat"));
            string ini = File.ReadAllText(Source("MiBTest.ini"))
                .Replace(RealFolder, tempDir.Path + Path.DirectorySeparatorChar);
            return tempDir.WriteFile("MiBTest.ini", ini);
        }

        private async Task<MiBTestConfig> LoadGoldenAsync()
        {
            var cfg = new MiBTestConfig(PrepareGoldenSet(), "1.0.0.0");
            var (rc, res) = await cfg.ReadConfigFileAsync();
            Assert.That(rc, Is.EqualTo(1), res);
            return cfg;
        }

        /// SC01: The whole real STI CLI set loads without error.
        [Test]
        public async Task RealStiCliConfig_ReadConfigFile_ReturnsSuccess()
        {
            var cfg = new MiBTestConfig(PrepareGoldenSet(), "1.0.0.0");

            var (rc, res) = await cfg.ReadConfigFileAsync();

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.HandlerCommType, Is.EqualTo(HandlerCommTypes.STICLIComm));
        }

        /// SC02: Sub-configurations are sections inside MiBTest.ini itself, not separate files.
        [Test]
        public async Task RealStiCliConfig_SubSections_AreReadFromMiBTestIniItself()
        {
            var gem = (await LoadGoldenAsync()).gemCliConfig;

            Assert.That(gem.VariableCfgPath, Is.EqualTo(tempDir.PathOf("MiBTest.ini")));
            Assert.That(gem.VariableCfgSecName, Is.EqualTo("CLIGemVariables"));
            Assert.That(gem.RemoteCmdCfgSecName, Is.EqualTo("CLIGemRemoteCommand"));
            Assert.That(gem.AttrReqCfgSecName, Is.EqualTo("CLIAttributeRequest"));
            Assert.That(gem.GemCliContainer, Is.EqualTo("HostComm"));
        }

        /// SC03: The real JSON (129 entries, one trailing comma at the end of the array) loads.
        [Test]
        public async Task RealStiCliConfig_JsonWithTrailingComma_LoadsAllVariables()
        {
            var gem = (await LoadGoldenAsync()).gemCliConfig;

            Assert.That(gem.JsonVidCfg.JsonGemVarCfg, Has.Count.EqualTo(129));
            Assert.That(gem.JsonVidCfg.GetCliGemVarSetting(311).VarName, Is.EqualTo("LotNumber"));
        }

        /// SC04: Output locations (remote command folder + extension, MMI window handle) are parsed.
        [Test]
        public async Task RealStiCliConfig_OutputLocations_AreParsed()
        {
            var gem = (await LoadGoldenAsync()).gemCliConfig;

            Assert.That(gem.RcmdDestPath, Is.EqualTo(@"D:\tScanSecsGem\STICLITester\RCMD"));
            Assert.That(gem.RcmdFileExt, Is.EqualTo(".dat"));
            Assert.That(gem.MmiWinHandleCfg.Container, Is.EqualTo("master"));
            Assert.That(gem.MmiWinHandleCfg.Section, Is.EqualTo("window handles"));
            Assert.That(gem.MmiWinHandleCfg.Variable, Is.EqualTo("MMI"));
        }

        /// SC05: The real attribute request file (3 object types, one with empty lists) is parsed.
        [Test]
        public async Task RealStiCliConfig_AttributeRequestFile_IsParsed()
        {
            var attr = (await LoadGoldenAsync()).gemCliConfig.AttrReqCfg;

            Assert.That(attr.Count, Is.EqualTo(3));
            Assert.That(attr.AttrReqSettings["LotOperation"].ObjSpec, Is.EqualTo("TNR"));
            Assert.That(attr.AttrReqSettings["LotOperation"].AttrIDs,
                Is.EqualTo(new[] { "Lot_No", "Dcc", "Operation_code", "EOH", "Unit_ID_List" }));
            Assert.That(attr.AttrReqSettings["JOBSPEC"].AttrIDCnt, Is.EqualTo(0));
        }

        /// SC06: CLI variables "Section, Variable, Type, Callback" are loaded.
        [Test]
        public async Task RealStiCliConfig_CliVariables_AreLoaded()
        {
            var gem = (await LoadGoldenAsync()).gemCliConfig;

            var setAlarm = gem.GemCliVarsCfg["SetAlarm"];
            Assert.That(setAlarm.SecName, Is.EqualTo("Alarm"));
            Assert.That(setAlarm.VarName, Is.EqualTo("Set"));
            Assert.That(setAlarm.PropertyChangedFnName, Is.EqualTo("GemAlarmSet_PropertyChanged"));
        }
    }
}