using ConfigurationManager.SECSAttribute;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for <see cref="SECSAttrReqCfg"/>, which reads one SECS Attribute request setting
    /// per INI section.
    /// </summary>
    [TestFixture]
    public class SECSAttrReqCfgTests
    {
        private TempDirectory tempDir;

        [SetUp]
        public void SetUp() => tempDir = new TempDirectory();

        [TearDown]
        public void TearDown() => tempDir.Dispose();

        private SECSAttrReqCfg ReadConfig(IniBuilder ini, out int rc, out string res)
        {
            var cfg = new SECSAttrReqCfg(tempDir.WriteFile("attrmsg.ini", ini.ToString()));
            (rc, res) = cfg.ReadConfig();
            return cfg;
        }

        [Test]
        public void ReadConfig_FileMissing_ReturnsMinus2()
        {
            var cfg = new SECSAttrReqCfg(tempDir.PathOf("missing.ini"));

            var (rc, res) = cfg.ReadConfig();

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain("missing.ini"));
            Assert.That(cfg.Count, Is.EqualTo(0));
        }

        [Test]
        public void ReadConfig_FullSection_ParsesAllFields()
        {
            var ini = new IniBuilder()
                .Set("Carrier", "OBJSPEC", "EQ1")
                .Set("Carrier", "OBJTYPE", "Carrier")
                .Set("Carrier", "ObjIDs", "C1,C2,C3")
                .Set("Carrier", "AttrIDs", "Capacity,Status");

            var cfg = ReadConfig(ini, out int rc, out string res);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.Count, Is.EqualTo(1));
            var setting = cfg.AttrReqSettings["Carrier"];
            Assert.That(setting.ObjSpec, Is.EqualTo("EQ1"));
            Assert.That(setting.ObjType, Is.EqualTo("Carrier"));
            Assert.That(setting.ObjIDs, Is.EqualTo(new[] { "C1", "C2", "C3" }));
            Assert.That(setting.ObjIDCnt, Is.EqualTo(3));
            Assert.That(setting.AttrIDs, Is.EqualTo(new[] { "Capacity", "Status" }));
            Assert.That(setting.AttrIDCnt, Is.EqualTo(2));
            Assert.That(setting.AttrQualCnt, Is.EqualTo(0));
        }

        [Test]
        public void ReadConfig_KeyNamesAreCaseInsensitive()
        {
            var ini = new IniBuilder()
                .Set("Lot", "objspec", "EQ1")
                .Set("Lot", "ObjType", "Lot")
                .Set("Lot", "OBJIDS", "L1");

            var cfg = ReadConfig(ini, out int rc, out string res);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.AttrReqSettings["Lot"].ObjSpec, Is.EqualTo("EQ1"));
            Assert.That(cfg.AttrReqSettings["Lot"].ObjIDs, Is.EqualTo(new[] { "L1" }));
        }

        [Test]
        public void ReadConfig_OptionalKeysMissing_UsesEmptyDefaults()
        {
            var ini = new IniBuilder().Set("Substrate", "OBJTYPE", "Substrate");

            var cfg = ReadConfig(ini, out int rc, out string res);

            Assert.That(rc, Is.EqualTo(1), res);
            var setting = cfg.AttrReqSettings["Substrate"];
            Assert.That(setting.ObjSpec, Is.Empty);
            Assert.That(setting.ObjIDs, Is.Empty);
            Assert.That(setting.AttrIDs, Is.Empty);
            Assert.That(setting.AttrQuals, Is.Empty);
        }

        [Test]
        public void ReadConfig_MultipleSections_AreKeyedByObjType()
        {
            var ini = new IniBuilder()
                .Set("A", "OBJTYPE", "Carrier")
                .Set("B", "OBJTYPE", "Lot");

            var cfg = ReadConfig(ini, out int rc, out string res);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.AttrReqSettings.Keys, Is.EquivalentTo(new[] { "Carrier", "Lot" }));
        }

        [Test]
        public void ReadConfig_DuplicateObjType_ReturnsMinus1()
        {
            var ini = new IniBuilder()
                .Set("A", "OBJTYPE", "Carrier")
                .Set("B", "OBJTYPE", "Carrier");

            ReadConfig(ini, out int rc, out string res);

            Assert.That(rc, Is.EqualTo(-1));
            Assert.That(res, Does.StartWith("EXCEPTION"));
        }

        /// <summary>
        /// KNOWN BUG: the qualifier loop checks <c>if (rc != 0)</c> after GetKeyData, but
        /// GetKeyData returns 1 on success, so any section with a QualAttrN key makes ReadConfig
        /// fail with rc=-3 and an empty reason. Expected: the qualifier is parsed.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public void ReadConfig_WithQualifierAttributes_ParsesQualifiers()
        {
            var ini = new IniBuilder()
                .Set("Carrier", "OBJTYPE", "Carrier")
                .Set("Carrier", "QualAttr0", "Status, Ready, 1")
                .Set("Carrier", "QualAttr1", "Capacity, 25, 3");

            var cfg = ReadConfig(ini, out int rc, out string res);

            Assert.That(rc, Is.EqualTo(1), res);
            var quals = cfg.AttrReqSettings["Carrier"].AttrQuals;
            Assert.That(quals, Has.Count.EqualTo(2));
            Assert.That(quals[0].AttrID, Is.EqualTo("Status"));
            Assert.That(quals[0].AttrData, Is.EqualTo("Ready"));
            Assert.That(quals[0].AttrReln, Is.EqualTo(1));
        }

        [Test]
        public void ToString_NoSettings_ReturnsEmpty()
        {
            Assert.That(new SECSAttrReqCfg("x.ini").ToString(), Is.Empty);
        }

        [Test]
        public void ToString_WithSettings_DescribesEachSetting()
        {
            var ini = new IniBuilder()
                .Set("Carrier", "OBJSPEC", "EQ1")
                .Set("Carrier", "OBJTYPE", "Carrier")
                .Set("Carrier", "ObjIDs", "C1");
            var cfg = ReadConfig(ini, out _, out _);

            string text = cfg.ToString();

            Assert.That(text, Does.Contain("SECS Attribute Request Message setting for: Carrier"));
            Assert.That(text, Does.Contain("ObjIDs:\"C1\""));
            Assert.That(text, Does.Contain("No AttrIDs defined"));
            Assert.That(text, Does.Contain("No Attribute Qualifiers defined"));
        }

        [Test]
        public void SECSAttrQualCfg_ToString_ListsAllFields()
        {
            var qual = new SECSAttrQualCfg { AttrID = "Status", AttrData = "Ready", AttrReln = 2 };

            Assert.That(qual.ToString(), Is.EqualTo("AttrID:Status, AttrData:Ready, AttrReln:2"));
        }
    }
}
