using System.Threading.Tasks;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for the Socket Comm only sections: SECS data types, remote command handlers,
    /// wafer map setup file items and the four map setup parameter sections.
    /// </summary>
    [TestFixture]
    public class MiBTestConfigSocketSettingsTests : MiBTestConfigTestBase
    {
        #region SECSDataTypeEnum

        [Test]
        public async Task GetSECSDataType_KnownCode_ReturnsMappedType()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetSECSDataType(2);

            Assert.That(rtn.rc, Is.EqualTo(1));
            Assert.That(rtn.res, Is.EqualTo("U4"));
        }

        [Test]
        public async Task GetSECSDataType_UnknownCode_ReturnsMinus3()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetSECSDataType(99);

            Assert.That(rtn.rc, Is.EqualTo(-3));
            Assert.That(rtn.res, Does.Contain("99"));
        }

        [Test]
        public void GetSECSDataType_BeforeRead_ReturnsMinus2()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            Assert.That(cfg.GetSECSDataType(1).rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task SECSDataTypeEnum_SectionMissing_ReturnsMinus2()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveSection("SECSDataTypeEnum");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain("SECSDataTypeEnum"));
        }

        // === Testing GetSECSDataTypeEnums() in ReadConfigFileAsync() ===
        [Test]
        public async Task SECSDataTypeEnum_NonNumericKey_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set("SECSDataTypeEnum", "abc", "A");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("abc").And.Contain("not numeric"));
        }

        // === Testing GetSECSDataTypeEnums() in ReadConfigFileAsync() ===
        [Test]
        public async Task SECSDataTypeEnum_EmptySection_ReturnsMinus4()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().ClearSection("SECSDataTypeEnum");

            var (_, rc, _) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
        }

        [Test]
        public async Task StiCliMode_DoesNotRequireSECSDataTypeEnum()
        {
            // Sanity check that the STI CLI branch never reaches GetSECSDataTypeEnums: the
            // failure must come from GemCliConfig (-3), not from the missing SECS section (-2).
            var ini = MiBTestConfigFixture.ValidSocketCommConfig()
                .Set(MiBTestConfigFixture.Dll, "CommType", "1")
                .Set(MiBTestConfigFixture.Dll, "STICLICommHandlerDLLFilePath", "a.dll")
                .Set(MiBTestConfigFixture.Dll, "STICLICommHandlerClassName", "A")
                .RemoveSection("SECSDataTypeEnum");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Not.Contain("SECSDataTypeEnum"));
        }

        #endregion

        #region RemoteCommandHandlers

        [Test]
        public async Task GetRemoteCommandHandlingFuncName_KnownCommand_ReturnsFunctionName()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetRemoteCommandHandlingFuncName("STOP");

            Assert.That(rtn.rc, Is.EqualTo(1));
            Assert.That(rtn.res, Is.EqualTo("StopCmd"));
        }

        [Test]
        public async Task GetRemoteCommandHandlingFuncName_UnknownCommand_ReturnsMinus3()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetRemoteCommandHandlingFuncName("PAUSE");

            Assert.That(rtn.rc, Is.EqualTo(-3));
            Assert.That(rtn.res, Does.Contain("PAUSE"));
        }

        [Test]
        public void GetRemoteCommandHandlingFuncName_BeforeRead_ReturnsMinus2()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            Assert.That(cfg.GetRemoteCommandHandlingFuncName("START").rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task RemoteCommandHandlers_SectionMissing_ReturnsMinus2()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveSection("RemoteCommandHandlers");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain("RemoteCommandHandlers"));
        }

        [Test]
        public async Task RemoteCommandHandlers_EmptySection_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().ClearSection("RemoteCommandHandlers");

            var (_, rc, _) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
        }

        #endregion

        #region WaferMapSetupFileItems

        [Test]
        public async Task WaferMapSetupFileItems_ValidSection_ParsesKeyNameAndTypeTrimmed()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.HandlerMapSetupFileItems, Has.Count.EqualTo(2));
            var item = cfg.HandlerMapSetupFileItems["WaferSize"];
            Assert.That(item.SetupFileKeyName, Is.EqualTo("WAFER_SIZE"));
            Assert.That(item.SECSDataType, Is.EqualTo("U4"));
        }

        [TestCase("WAFER_SIZE")]
        [TestCase("WAFER_SIZE,U4,EXTRA")]
        public async Task WaferMapSetupFileItems_WrongNumberOfParts_ReturnsMinus3(string value)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set("WaferMapSetupFileItems", "WaferSize", value);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("WaferSize"));
        }

        [Test]
        public async Task WaferMapSetupFileItems_EmptySection_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().ClearSection("WaferMapSetupFileItems");

            var (_, rc, _) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
        }

        [Test]
        public async Task WaferMapSetupFileItems_SectionMissing_ReturnsMinus2()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveSection("WaferMapSetupFileItems");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain("WaferMapSetupFileItems").And.Contain("missing"));
        }

        #endregion

        #region Map setup parameter sections

        private static readonly string[] MapParamSections =
        {
            "MapSetupDataRequestParams",
            "MapSetupDataSendParams",
            "MapTransmitInquireParams",
            "MapDataType2SendParams",
        };

        [Test]
        public async Task MapSetupDataRequestParams_ValidSection_ParsesTypesIndicesAndConvFn()
        {
            var cfg = await LoadValidAsync();
            var settings = cfg.MapSetupDataRequestParams;

            Assert.That(settings.SectionName, Is.EqualTo("MapSetupDataRequestParams"));
            Assert.That(settings.ConfigFileName, Is.EqualTo(cfg.ConfigFilePath));
            Assert.That(settings.Params, Has.Count.EqualTo(2));

            var waferId = settings.Params[0];
            Assert.That(waferId.Name, Is.EqualTo("WaferId"));
            Assert.That(waferId.Type, Is.EqualTo(typeof(string)));
            Assert.That(waferId.RowIndices.Start, Is.EqualTo(1));
            Assert.That(waferId.RowIndices.End, Is.EqualTo(1));
            Assert.That(waferId.ConvertFuncName, Is.EqualTo("None"));

            var rowCount = settings.Params[1];
            Assert.That(rowCount.Type, Is.EqualTo(typeof(int)));
            Assert.That(rowCount.RowIndices.Start, Is.EqualTo(2));
            Assert.That(rowCount.RowIndices.End, Is.EqualTo(3));
            Assert.That(rowCount.ConvertFuncName, Is.EqualTo("ToRowCount"));
        }

        [Test]
        public async Task MapParamSections_AllValid_ArePopulated()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.MapSetupDataSendParams.Params, Has.Count.EqualTo(1));
            Assert.That(cfg.MapTransmitInquireParams.Params, Has.Count.EqualTo(1));
            Assert.That(cfg.MapDataType2SendParams.Params, Has.Count.EqualTo(1));
        }

        [TestCaseSource(nameof(MapParamSections))]
        public async Task MapParamSection_Missing_ReturnsMinus2(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveSection(section);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain(section));
        }

        [TestCaseSource(nameof(MapParamSections))]
        public async Task MapParamSection_Empty_ReturnsMinus3(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().ClearSection(section);

            var (_, rc, _) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
        }

        [TestCaseSource(nameof(MapParamSections))]
        public async Task MapParamSection_TooFewParts_ReturnsMinus4(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(section, "Bad", "System.String,1");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("Invalid format"));
        }

        [TestCaseSource(nameof(MapParamSections))]
        public async Task MapParamSection_NonNumericStartIndex_ReturnsMinus4(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(section, "Bad", "System.String,x,1");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("start index"));
        }

        [TestCaseSource(nameof(MapParamSections))]
        public async Task MapParamSection_NonNumericEndIndex_ReturnsMinus4(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(section, "Bad", "System.String,1,y");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("end index"));
        }

        [TestCaseSource(nameof(MapParamSections))]
        public async Task MapParamSection_UnknownType_ReturnsMinus4(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(section, "Bad", "Foo.Bar,1,1");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("Failed to get the type of Bad"));
        }

        #endregion
    }
}
