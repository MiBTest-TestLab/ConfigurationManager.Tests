using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Golden-file tests: load a frozen copy of the real MiBTest.ini end to end.
    /// The fixture tests check the reading rules; these check the real configuration works.
    /// </summary>
    [TestFixture]
    public class MiBTestConfigGoldenFileTests
    {
        private static string GoldenIni =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "MiBTest.ini");

        private static async Task<MiBTestConfig> LoadGoldenAsync()
        {
            var cfg = new MiBTestConfig(GoldenIni, "1.0.0.0");
            var (rc, res) = await cfg.ReadConfigFileAsync();
            Assert.That(rc, Is.EqualTo(1), res);
            return cfg;
        }

        /// GC01: The whole real file loads without error. 
        [Test]
        public async Task RealConfig_ReadConfigFile_ReturnsSuccess()
        {
            var cfg = new MiBTestConfig(GoldenIni, "1.0.0.0");

            var (rc, res) = await cfg.ReadConfigFileAsync();

            Assert.That(rc, Is.EqualTo(1), res);
        }

        /// GC02: [DLL] values match the real file.
        [Test]
        public async Task RealConfig_DllSection_MatchesFile()
        {
            var cfg = await LoadGoldenAsync();

            Assert.That(cfg.HandlerCommType, Is.EqualTo(HandlerCommTypes.SocketComm));
            Assert.That(cfg.MiBDllFilePath, Is.EqualTo(@"D:\iFocus-SECSGEM\CSMiBTest\MiB.dll"));
            Assert.That(cfg.MiBClassName, Is.EqualTo("MiB.MachineInfoBridge"));
        }

        /// GC03: ';' and ',' are read as delimiter values, not as comments.
        [Test]
        public async Task RealConfig_SemicolonAndCommaDelimiters_AreReadAsValues()
        {
            var cfg = await LoadGoldenAsync();

            Assert.That(cfg.GetFrameDelimiter("HandleCommEnableCmd").res, Is.EqualTo(";"));
            Assert.That(cfg.GetNestedFrameDelimiter("HandleProcessingMgmtCmd").res, Is.EqualTo(","));
        }

        /// GC04: Hex delimiters 0x1e / 0x1f are converted to their ASCII characters.
        [Test]
        public async Task RealConfig_HexDelimiters_AreConverted()
        {
            var cfg = await LoadGoldenAsync();

            Assert.That(cfg.GetFrameDelimiter("HandleSetNestedListTypeVidValueCmd").res, Is.EqualTo("\u001e"));
            Assert.That(cfg.GetNestedFrameDelimiter("HandleSetNestedListTypeVidValueCmd").res, Is.EqualTo("\u001f"));
        }

        /// GC05: When several sections share the same first frame, the FIRST one in the file wins.
        /// Section order in MiBTest.ini is part of the protocol.
        [TestCase("Z", "HandleMiscDataRequestCmd")]
        [TestCase("P", "HandleProcessingMgmtCmd")]
        [TestCase("K", "HandleE87Cmd")]
        [TestCase("M", "HandleCommEnableCmd")]
        public async Task RealConfig_SharedFirstFrame_ResolvesToFirstSectionInFile(string cmd, string expected)
        {
            var cfg = await LoadGoldenAsync();

            Assert.That(cfg.GetHandlingFunctionName(cmd).res, Is.EqualTo(expected));
        }

        /// GC06: Function sub-sections are mapped by function number.
        [Test]
        public async Task RealConfig_FunctionSubSection_IsFoundByNumber()
        {
            var cfg = await LoadGoldenAsync();

            Assert.That(cfg.GetMsgSubSectionName("HandleProcessingMgmtCmd", 5).res,
                Is.EqualTo("HandleProcessJobCommandCmd"));
        }

        /// GC07: Real state names with spaces, hyphens and braces are read correctly.
        [Test]
        public async Task RealConfig_StateNamesWithSpaces_AreReadCorrectly()
        {
            var cfg = await LoadGoldenAsync();

            Assert.That(cfg.GetControlStateNumber("ON-LINE LOCAL").Item1, Is.EqualTo(4));
            Assert.That(cfg.GetLinkStateNumber("ENABLED {NOT COMMUNICATING}").Item1, Is.EqualTo(1));
            Assert.That(cfg.GetSECSDataType(6).res, Is.EqualTo("A"));
        }
    }
}