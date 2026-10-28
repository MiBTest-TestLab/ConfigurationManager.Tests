using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for <see cref="GemCliConfig"/>, the STI CLI SECS/GEM part of the MiBTest config.
    /// Uses real files in a temp folder because GemCliConfig opens the variable, remote command,
    /// terminal request and attribute request config files by path.
    /// </summary>
    [TestFixture]
    public class GemCliConfigTests
    {
        private const string Main = GemCliConfigFixture.MainSection;

        private TempDirectory tempDir;
        private GemCliConfigFixture fixture;

        [SetUp]
        public void SetUp()
        {
            tempDir = new TempDirectory();
            fixture = new GemCliConfigFixture(tempDir);
        }

        [TearDown]
        public void TearDown() => tempDir.Dispose();

        private (GemCliConfig cfg, int rc, string res) Read()
        {
            var cfg = fixture.Build();
            var rtn = cfg.ReadGemCliConfig();
            return (cfg, rtn.rc, rtn.res);
        }

        private GemCliConfig ReadValid()
        {
            var (cfg, rc, res) = Read();
            Assert.That(rc, Is.EqualTo(1), res);
            return cfg;
        }

        #region Success path

        [Test]
        public void ReadGemCliConfig_ValidConfig_ReturnsSuccess()
        {
            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(res, Is.Empty);
        }

        [Test]
        public void ReadGemCliConfig_ValidConfig_PopulatesMainSectionSettings()
        {
            var cfg = ReadValid();

            Assert.That(cfg.CLICachePath, Is.EqualTo(@"C:\CLI\Cache"));
            Assert.That(cfg.EventCfgPath, Is.EqualTo(tempDir.PathOf("events.ini")));
            Assert.That(cfg.EventCfgSecName, Is.EqualTo("Events"));
            Assert.That(cfg.VariableCfgPath, Is.EqualTo(tempDir.PathOf("vars.ini")));
            Assert.That(cfg.VariableCfgSecName, Is.EqualTo("GemVar"));
            Assert.That(cfg.RemoteCmdCfgPath, Is.EqualTo(tempDir.PathOf("rcmd.ini")));
            Assert.That(cfg.RemoteCmdCfgSecName, Is.EqualTo("Rcmd"));
            Assert.That(cfg.GemCliContainer, Is.EqualTo("SECSGEM"));
        }

        [Test]
        public void ReadGemCliConfig_ValidConfig_LoadsJsonVidConfigAndVarGetFolder()
        {
            var cfg = ReadValid();

            Assert.That(cfg.CLIGemVarJsonFilePath, Is.EqualTo(tempDir.PathOf("vids.json")));
            Assert.That(cfg.JsonVidFileContents, Is.EqualTo(GemCliConfigFixture.ValidJson));
            Assert.That(cfg.JsonVidCfg.GetCliGemVarSetting("LotId").VID, Is.EqualTo(1001));
            Assert.That(cfg.GemVarGetFolder, Is.EqualTo(@"C:\VarGet\"), "a trailing separator is appended");
        }

        [TestCase(@"C:\VarGet\")]
        [TestCase("C:/VarGet/")]
        public void VariableGetFolder_AlreadyEndsWithSeparator_IsNotDoubled(string folder)
        {
            fixture.VariableCfg.Set("GemVar", "VariableGetFolder", folder);

            var cfg = ReadValid();

            Assert.That(cfg.GemVarGetFolder, Is.EqualTo(folder));
        }

        [Test]
        public void ReadGemCliConfig_ValidConfig_ParsesRemoteCommandDestination()
        {
            var cfg = ReadValid();

            Assert.That(cfg.RcmdDestPath, Is.EqualTo(@"C:\RCMD"));
            Assert.That(cfg.RcmdFileExt, Is.EqualTo(".txt"), "a leading '.' is added to the extension");
        }

        [Test]
        public void RemoteCommandDestination_WithoutExtension_KeepsDefaultExtension()
        {
            fixture.RcmdCfg.Set("Rcmd", "RCMDDestPath", @"C:\RCMD");

            var cfg = ReadValid();

            Assert.That(cfg.RcmdFileExt, Is.EqualTo(".dat"));
        }

        /// <summary>
        /// KNOWN BUG: GetRcmdConfig calls RcmdDestPath.Remove(...) but discards the result, so a
        /// trailing directory separator is never actually stripped.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public void RemoteCommandDestination_TrailingSeparator_ShouldBeRemoved()
        {
            fixture.RcmdCfg.Set("Rcmd", "RCMDDestPath", @"C:\RCMD\,dat");

            var cfg = ReadValid();

            Assert.That(cfg.RcmdDestPath, Is.EqualTo(@"C:\RCMD"));
        }

        /// <summary>
        /// KNOWN BUG: GetGemCliComponentConfig does not trim the section name after the comma,
        /// so "vars.ini, GemVar" looks for section " GemVar" and fails with -8.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public void ComponentConfig_SpaceAfterComma_SectionNameIsTrimmed()
        {
            fixture.Main.Set(Main, "VariableConfig", $"{tempDir.PathOf("vars.ini")}, GemVar");

            var (cfg, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.VariableCfgSecName, Is.EqualTo("GemVar"));
        }

        [Test]
        public void TerminalRequestDestination_FullPath_IsSplitIntoFolderAndFileName()
        {
            var cfg = ReadValid();

            Assert.That(cfg.TermReqDestPath, Is.EqualTo(@"C:\Term\Out"));
            Assert.That(cfg.TermReqFilename, Is.EqualTo("TermReq.dat"));
        }

        [Test]
        public void TerminalRequestDestination_FileNameOnly_HasEmptyFolder()
        {
            fixture.TermReqCfg.Set("TermReq", "TerminalReqDestPath", "Requests.dat");

            var cfg = ReadValid();

            Assert.That(cfg.TermReqDestPath, Is.Empty);
            Assert.That(cfg.TermReqFilename, Is.EqualTo("Requests.dat"));
        }

        [Test]
        public void ReadGemCliConfig_ValidConfig_LoadsAttributeRequestConfig()
        {
            var cfg = ReadValid();

            Assert.That(cfg.AttrReqConfigFile, Is.EqualTo(tempDir.PathOf("attrreq.ini")));
            Assert.That(cfg.AttrReqCfgSecName, Is.EqualTo("AttrReq"));
            Assert.That(cfg.AttrReqCfgPath, Is.EqualTo(tempDir.PathOf("attrmsg.ini")));
            Assert.That(cfg.AttrDataFolder, Is.EqualTo(@"C:\AttrData\"));
            Assert.That(cfg.AttrReqCfg.Count, Is.EqualTo(1));
            Assert.That(cfg.AttrReqCfg.AttrReqSettings["Carrier"].ObjIDs, Is.EqualTo(new[] { "C1", "C2" }));
        }

        [Test]
        public void ReadGemCliConfig_ValidConfig_ParsesMmiWindowHandleLocation()
        {
            var cfg = ReadValid();

            Assert.That(cfg.MmiWinHandleCfg.Container, Is.EqualTo("MMI"));
            Assert.That(cfg.MmiWinHandleCfg.Section, Is.EqualTo("Window"));
            Assert.That(cfg.MmiWinHandleCfg.Variable, Is.EqualTo("Handle"));
        }

        [Test]
        public void ReadGemCliConfig_ValidConfig_LoadsEveryCliVariable()
        {
            var cfg = ReadValid();

            Assert.That(cfg.GemCliVarsCfg.Keys, Is.EquivalentTo(GemCliConfigFixture.CliVarKeys));
        }

        #endregion

        #region GetCliVariable / LookupCliEventCEID

        [Test]
        public void GetCliVariable_KnownVariable_ReturnsTrimmedSettings()
        {
            var cfg = ReadValid();

            var (cliVar, res) = cfg.GetCliVariable("SendEvent");

            Assert.That(res, Is.Empty);
            Assert.That(cliVar.SecName, Is.EqualTo("Events"));
            Assert.That(cliVar.VarName, Is.EqualTo("SendEvent"));
            Assert.That(cliVar.VarType, Is.EqualTo("Bool"));
            Assert.That(cliVar.PropertyChangedFnName, Is.EqualTo("OnSendEvent"));
        }

        [Test]
        public void GetCliVariable_WithoutOptionalFunctionName_HasEmptyFunctionName()
        {
            var cfg = ReadValid();

            var (cliVar, _) = cfg.GetCliVariable("MiBStart");

            Assert.That(cliVar.PropertyChangedFnName, Is.Empty);
        }

        [Test]
        public void GetCliVariable_UnknownVariable_ReturnsNullWithError()
        {
            var cfg = ReadValid();

            var (cliVar, res) = cfg.GetCliVariable("NoSuchVar");

            Assert.That(cliVar, Is.Null);
            Assert.That(res, Does.StartWith("ERROR").And.Contain("NoSuchVar"));
        }

        [Test]
        public void GetCliVariable_BeforeRead_ReturnsNullWithException()
        {
            var cfg = fixture.Build();

            var (cliVar, res) = cfg.GetCliVariable("MiBStart");

            Assert.That(cliVar, Is.Null);
            Assert.That(res, Does.StartWith("EXCEPTION"));
        }

        [Test]
        public void LookupCliEventCEID_EventTableNoLongerLoaded_ReturnsMinus2()
        {
            // Since ver 1.0.0.9 the CLI events to CEID table is not read any more.
            var cfg = ReadValid();

            var (rc, _) = cfg.LookupCliEventCEID("AnyEvent");

            Assert.That(rc, Is.EqualTo(-2));
        }

        #endregion

        #region Error paths - main section

        [Test]
        public void ReadGemCliConfig_NullIniData_ReturnsMinus2()
        {
            var rtn = new GemCliConfig(null).ReadGemCliConfig();

            Assert.That(rtn.rc, Is.EqualTo(-2));
        }

        [Test]
        public void ReadGemCliConfig_MainSectionMissing_ReturnsMinus3()
        {
            var cfg = new GemCliConfig(GemCliConfigFixture.Parse(new IniBuilder().Set("Other", "a", "b")));

            var rtn = cfg.ReadGemCliConfig();

            Assert.That(rtn.rc, Is.EqualTo(-3));
            Assert.That(rtn.res, Does.Contain(Main));
        }

        [TestCase("CLIPath", -4)]
        [TestCase("EventConfig", -5)]
        [TestCase("VariableConfig", -7)]
        [TestCase("RemoteCommandConfig", -8)]
        [TestCase("TerminalRequestConfig", -8)]
        [TestCase("AttributeRequestConfig", -8)]
        [TestCase("CLIMmiWinHandle", -5)]
        [TestCase("Container", -10)]
        [TestCase("MiBStart", -11)]
        [TestCase("GetAttributes", -11)]
        public void ReadGemCliConfig_MissingMainKey_ReturnsErrorCode(string key, int expectedRc)
        {
            fixture.Main.RemoveKey(Main, key);

            var (_, rc, _) = Read();

            Assert.That(rc, Is.EqualTo(expectedRc));
        }

        [Test]
        public void ReadGemCliConfig_CliVariableWithTooFewParts_ReturnsMinus12()
        {
            fixture.Main.Set(Main, "AlarmID", "Section, Name");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-12));
            Assert.That(res, Does.Contain("AlarmID"));
        }

        [Test]
        public void ReadGemCliConfig_MmiWindowHandleWithTooFewParts_ReturnsMinus5()
        {
            fixture.Main.Set(Main, "CLIMmiWinHandle", "MMI/Window");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-5));
            Assert.That(res, Does.Contain("MMI/Window"));
        }

        [Test]
        public void ReadGemCliConfig_CalledTwice_FailsBecauseVariablesAreAlreadyAdded()
        {
            // Documents current behaviour: the variable dictionary is not cleared between reads.
            var cfg = ReadValid();

            var second = cfg.ReadGemCliConfig();

            Assert.That(second.rc, Is.EqualTo(-1));
        }

        #endregion

        #region Error paths - referenced files

        [Test]
        public void VariableConfigFile_Missing_ReturnsMinus8()
        {
            fixture.VariableCfg = null;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain("vars.ini").And.Contain("not found"));
        }

        [Test]
        public void VariableConfigFile_SectionMissing_ReturnsMinus8()
        {
            fixture.Main.Set(Main, "VariableConfig", $"{tempDir.PathOf("vars.ini")},WrongSection");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain("WrongSection"));
        }

        [TestCase("JsonFilePath")]
        [TestCase("VariableGetFolder")]
        public void VariableConfigFile_KeyMissing_ReturnsMinus8(string key)
        {
            fixture.VariableCfg.RemoveKey("GemVar", key);

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain(key));
        }

        [Test]
        public void JsonFile_Missing_ReturnsMinus8()
        {
            fixture.Json = null;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain("vids.json").And.Contain("not found"));
        }

        [Test]
        public void JsonFile_Empty_ReturnsMinus8()
        {
            fixture.Json = string.Empty;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain("empty"));
        }

        [Test]
        public void JsonFile_Invalid_ReturnsMinus8WithException()
        {
            fixture.Json = "{ not json";

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.StartWith("EXCEPTION"));
        }

        [Test]
        public void RemoteCommandConfigFile_Missing_ReturnsMinus9()
        {
            fixture.RcmdCfg = null;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("rcmd.ini"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void RemoteCommandConfigFile_DestPathMissingOrEmpty_ReturnsMinus9(string value)
        {
            if (value is null) fixture.RcmdCfg.RemoveKey("Rcmd", "RCMDDestPath");
            else fixture.RcmdCfg.Set("Rcmd", "RCMDDestPath", value);

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("RCMDDestPath"));
        }

        [Test]
        public void RemoteCommandConfigFile_SectionMissing_ReturnsMinus9()
        {
            fixture.RcmdCfg = new IniBuilder().Set("Other", "RCMDDestPath", @"C:\RCMD");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("Rcmd").And.Contain("missing"));
        }

        [Test]
        public void TerminalRequestConfigFile_Missing_ReturnsMinus9()
        {
            fixture.TermReqCfg = null;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("termreq.ini"));
        }

        [Test]
        public void TerminalRequestConfigFile_DestPathMissing_ReturnsMinus9()
        {
            fixture.TermReqCfg.RemoveKey("TermReq", "TerminalReqDestPath");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("TerminalReqDestPath"));
        }

        [Test]
        public void TerminalRequestConfigFile_SectionMissing_ReturnsMinus9()
        {
            fixture.TermReqCfg = new IniBuilder().Set("Other", "TerminalReqDestPath", @"C:\Term\TermReq.dat");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("TermReq").And.Contain("missing"));
        }

        [Test]
        public void TerminalRequestConfigFile_DestPathEmpty_ReturnsMinus9()
        {
            fixture.TermReqCfg.Set("TermReq", "TerminalReqDestPath", "");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-9));
            Assert.That(res, Does.Contain("cannot be empty"));
        }

        [Test]
        public void AttributeRequestConfigFile_Missing_ReturnsMinus4()
        {
            fixture.AttrReqCfg = null;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("attrreq.ini"));
        }

        [TestCase("AttributRequestConfigPath")]
        [TestCase("AttributeDataFolder")]
        public void AttributeRequestConfigFile_KeyMissing_ReturnsMinus4(string key)
        {
            fixture.AttrReqCfg.RemoveKey("AttrReq", key);

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain(key));
        }

        [Test]
        public void AttributeRequestConfigFile_SectionMissing_ReturnsMinus4()
        {
            fixture.AttrReqCfg = new IniBuilder()
                .Set("Other", "AttributRequestConfigPath", tempDir.PathOf("attrmsg.ini"))
                .Set("Other", "AttributeDataFolder", @"C:\AttrData");

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("AttrReq").And.Contain("not found"));
        }

        [Test]
        public void AttributeRequestMessageFile_Missing_ReturnsMinus4()
        {
            fixture.AttrReqMsgCfg = null;

            var (_, rc, res) = Read();

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("attrmsg.ini"));
        }

        #endregion

        [Test]
        public void ToString_AfterRead_ContainsSettingsAndVariables()
        {
            var cfg = ReadValid();

            string text = cfg.ToString();

            Assert.That(text, Does.Contain(@"CLICachePath=C:\CLI\Cache"));
            Assert.That(text, Does.Contain("GemCliContainer=SECSGEM"));
            Assert.That(text, Does.Contain("Name: SendEvent"));
            Assert.That(text, Does.Contain("SECS Attribute Request Message setting for: Carrier"));
        }

        [Test]
        public void ToString_BeforeRead_DoesNotThrow()
        {
            Assert.That(() => fixture.Build().ToString(), Throws.Nothing);
        }
    }
}
