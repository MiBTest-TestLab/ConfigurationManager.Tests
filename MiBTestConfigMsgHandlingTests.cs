using System;
using System.Linq;
using System.Threading.Tasks;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for the "*Cmd" message handling sections (ExtractHandlingMethodCfgAsync) and
    /// every lookup method built on top of MsgHandlingCfg.
    /// </summary>
    /// <remarks>
    /// The fixture config contains three message sections:
    /// <list type="bullet">
    /// <item>LotStartCmd       - ReceiveMsg, delimiter 0x7C ('|'), nested '^', 2 frames, function and sub-command sections</item>
    /// <item>LotStartReplyCmd  - ReplyMsg, delimiter '|', no nested delimiter, no Reply key</item>
    /// <item>AlarmEventCmd     - ReplyWithEvent, EventName1/EventName2 only</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class MiBTestConfigMsgHandlingTests : MiBTestConfigTestBase
    {
        private const string LotStart = "LotStartCmd";
        private const string LotStartReply = "LotStartReplyCmd";
        private const string AlarmEvent = "AlarmEventCmd";

        #region Extraction

        [Test]
        public async Task ExtractHandlingMethodCfgAsync_ReturnsNumberOfSectionsFound()
        {
            var cfg = await LoadValidAsync();

            var (rc, res) = await cfg.ExtractHandlingMethodCfgAsync();

            Assert.That(rc, Is.EqualTo(3), res);
            Assert.That(cfg.MsgHandlingCfg.Keys, Is.EquivalentTo(new[] { LotStart, LotStartReply, AlarmEvent }));
        }

        [Test]
        public async Task ReceiveSection_AllSettingsExtracted()
        {
            var cfg = await LoadValidAsync();

            var lotStart = cfg.MsgHandlingCfg[LotStart];

            Assert.That(lotStart.MethodName, Is.EqualTo(LotStart));
            Assert.That(lotStart.CommType, Is.EqualTo(CommTypes.ReceiveMsg));
            Assert.That(lotStart.FrameDelimiter, Is.EqualTo("|"), "0x7C should be converted to '|'");
            Assert.That(lotStart.NestedDelimiter, Is.EqualTo("^"));
            Assert.That(lotStart.FrameCount, Is.EqualTo(2));
            Assert.That(lotStart.VariableFrames, Is.False);
            Assert.That(lotStart.FrameData, Is.EqualTo(new[] { "LOTSTART", "A,B,C" }));
            Assert.That(lotStart.ReplySecName, Is.EqualTo(LotStartReply));
            Assert.That(lotStart.FuncSections[1], Is.EqualTo("LotStartF1Cmd"));
            Assert.That(lotStart.SubCmdSections["RECIPE"], Is.EqualTo("LotStartRecipeCmd"));
        }

        [Test]
        public async Task FunctionSectionKeys_AreNotTreatedAsSubCommands()
        {
            var cfg = await LoadValidAsync();

            var lotStart = cfg.MsgHandlingCfg[LotStart];

            Assert.That(lotStart.FuncSections.Keys, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(lotStart.SubCmdSections.Keys, Is.EquivalentTo(new[] { "RECIPE", "RCP_KEY", "EMPTY" }));
        }

        [Test]
        public async Task ReplySection_WithoutNestedDelimiterOrReplyKey_UsesEmptyStrings()
        {
            var cfg = await LoadValidAsync();

            var reply = cfg.MsgHandlingCfg[LotStartReply];

            Assert.That(reply.CommType, Is.EqualTo(CommTypes.ReplyMsg));
            Assert.That(reply.NestedDelimiter, Is.Empty);
            Assert.That(reply.ReplySecName, Is.Empty, "A missing Reply key is not treated as an error");
            Assert.That(reply.VariableFrames, Is.True);
            Assert.That(reply.FuncSections, Is.Empty);
            Assert.That(reply.SubCmdSections, Is.Empty);
        }

        [Test]
        public async Task ReplyWithEventSection_OnlyEventNamesAreExtractedAndTrimmed()
        {
            var cfg = await LoadValidAsync();

            var alarm = cfg.MsgHandlingCfg[AlarmEvent];

            Assert.That(alarm.CommType, Is.EqualTo(CommTypes.ReplyWithEvent));
            Assert.That(alarm.EventNames, Is.EqualTo(new[] { "AlarmSet", "AlarmClear" }));
            Assert.That(alarm.FrameDelimiter, Is.Null);
            Assert.That(alarm.FrameData, Is.Null);
        }

        [Test]
        public async Task NoCmdSections_ReturnsMinus8()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig()
                .RemoveSection(LotStart).RemoveSection(LotStartReply).RemoveSection(AlarmEvent);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain("No message handling function sections"));
        }

        [TestCase(null, -2, "Missing CommType")]
        [TestCase("abc", -3, "not numeric")]
        [TestCase("9", -4, "invalid")]
        public async Task CommType_Invalid_ReturnsErrorCode(string value, int expectedRc, string expectedText)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig();
            if (value is null) ini.RemoveKey(LotStart, "CommType");
            else ini.Set(LotStart, "CommType", value);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(expectedRc));
            Assert.That(res, Does.Contain(expectedText).And.Contain(LotStart));
        }

        [Test]
        public async Task FrameDelimiter_Missing_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveKey(LotStart, "FrameDelimiter");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("FrameDelimiter"));
        }

        [TestCase("0x09", "\t")]
        [TestCase("0x2C", ",")]
        [TestCase("0x3b", ";")]
        [TestCase("~", "~")]
        public async Task FrameDelimiter_HexOrLiteral_IsConverted(string value, string expected)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(LotStart, "FrameDelimiter", value);

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.MsgHandlingCfg[LotStart].FrameDelimiter, Is.EqualTo(expected));
        }

        [Test]
        public async Task FrameDelimiter_InvalidHex_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(LotStart, "FrameDelimiter", "0xZZ");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("0xZZ"));
        }

        [Test]
        public async Task NestedFrameDelimiter_Hex_IsConverted()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(LotStart, "NestedFrameDelimiter", "0x5E");

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.MsgHandlingCfg[LotStart].NestedDelimiter, Is.EqualTo("^"));
        }

        [Test]
        public async Task NestedFrameDelimiter_InvalidHex_ReturnsMinus8()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(LotStart, "NestedFrameDelimiter", "0xQQ");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-8));
            Assert.That(res, Does.Contain("0xQQ"));
        }

        [TestCase(null)]
        [TestCase("two")]
        [TestCase("-1")]
        public async Task MinFrameCount_MissingNonNumericOrNegative_ReturnsMinus4(string value)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig();
            if (value is null) ini.RemoveKey(LotStart, "MinFrameCount");
            else ini.Set(LotStart, "MinFrameCount", value);

            var (_, rc, _) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
        }

        [TestCase(null)]
        [TestCase("yes")]
        [TestCase("2")]
        public async Task VariableFrames_MissingNonNumericOrNotZeroOne_ReturnsMinus5(string value)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig();
            if (value is null) ini.RemoveKey(LotStart, "VariableFrames");
            else ini.Set(LotStart, "VariableFrames", value);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-5));
            Assert.That(res, Does.Contain("VariableFrames"));
        }

        [Test]
        public async Task FrameData_FewerThanMinFrameCount_ReturnsMinus6()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(LotStart, "MinFrameCount", "3");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-6));
            Assert.That(res, Does.Contain("less than the minimum"));
        }

        [Test]
        public async Task FrameData_KeysWithNonNumericSuffix_AreIgnored()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set(LotStart, "FrameDataX", "IGNORED");

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.MsgHandlingCfg[LotStart].FrameData, Does.Not.Contain("IGNORED"));
        }

        /// <summary>
        /// KNOWN BUG: ExtractFrameData keeps the order in which the keys appear in the file,
        /// not the N in FrameDataN, so FrameData2 written before FrameData1 swaps their meaning.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public async Task FrameData_KeysOutOfOrderInFile_AreOrderedByNumber()
        {
            // Remove FrameData1 and add it back: IniBuilder appends it after FrameData2.
            var ini = MiBTestConfigFixture.ValidSocketCommConfig()
                .RemoveKey(LotStart, "FrameData1")
                .Set(LotStart, "FrameData1", "LOTSTART");

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.GetFrameData(LotStart, 0).res, Is.EqualTo("LOTSTART"));
        }

        [Test]
        public async Task SectionNotEndingWithCmd_IsNotTreatedAsMessageSection()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set("SomethingElse", "CommType", "bogus");

            var (cfg, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(1), res);
            Assert.That(cfg.MsgHandlingCfg.ContainsKey("SomethingElse"), Is.False);
        }

        #endregion

        #region Frame delimiter / frame data lookups

        [Test]
        public async Task FindFrameDelimiter_MessageContainsDelimiter_ReturnsDelimiter()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.FindFrameDelimiter("LOTSTART|A");

            Assert.That(rtn.rc, Is.EqualTo(1));
            Assert.That(rtn.res, Is.EqualTo("|"));
        }

        [Test]
        public async Task FindFrameDelimiter_NoDelimiterInMessage_ReturnsMinus2()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.FindFrameDelimiter("LOTSTART A").rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task GetFrameDelimiter_KnownAndUnknownFunction()
        {
            var cfg = await LoadValidAsync();

            var known = cfg.GetFrameDelimiter(LotStart);
            Assert.That(known.rc, Is.EqualTo(1));
            Assert.That(known.res, Is.EqualTo("|"));

            Assert.That(cfg.GetFrameDelimiter("NoSuchCmd").rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task GetNestedFrameDelimiter_KnownAndUnknownFunction()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetNestedFrameDelimiter(LotStart).res, Is.EqualTo("^"));

            var reply = cfg.GetNestedFrameDelimiter(LotStartReply);
            Assert.That(reply.rc, Is.EqualTo(1));
            Assert.That(reply.res, Is.Empty);

            Assert.That(cfg.GetNestedFrameDelimiter("NoSuchCmd").rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task GetMinFrameCount_ReturnsCountAsResultCode()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetMinFrameCount(LotStart).rc, Is.EqualTo(2));
            Assert.That(cfg.GetMinFrameCount("NoSuchCmd").rc, Is.EqualTo(-2));
        }

        [Test]
        public void GetMinFrameCount_BeforeRead_ReturnsMinus1()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            Assert.That(cfg.GetMinFrameCount(LotStart).rc, Is.EqualTo(-1));
        }

        [TestCase(0, 1, "LOTSTART")]
        [TestCase(1, 1, "A,B,C")]
        public async Task GetFrameData_ValidIndex_ReturnsFrameData(int index, int expectedRc, string expected)
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetFrameData(LotStart, index);

            Assert.That(rtn.rc, Is.EqualTo(expectedRc));
            Assert.That(rtn.res, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetFrameData_IndexOutOfRangeOrUnknownFunction_ReturnsError()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetFrameData(LotStart, 2).rc, Is.EqualTo(-3));
            Assert.That(cfg.GetFrameData(LotStart, -1).rc, Is.EqualTo(-1));
            Assert.That(cfg.GetFrameData("NoSuchCmd", 0).rc, Is.EqualTo(-2));
        }

        [TestCase("A", true)]
        [TestCase("C", true)]
        [TestCase("D", false)]
        [TestCase("A,B", false)]
        public async Task ValidateFrameAllowedValues_ChecksCommaSeparatedList(string received, bool expected)
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.ValidateFrameAllowedValues(LotStart, 1, received), Is.EqualTo(expected));
        }

        [Test]
        public async Task ValidateFrameAllowedValues_UnknownFunction_Throws()
        {
            var cfg = await LoadValidAsync();

            Assert.Throws<ApplicationException>(() => cfg.ValidateFrameAllowedValues("NoSuchCmd", 0, "A"));
        }

        #endregion

        #region Handling function name lookups

        [Test]
        public async Task GetHandlingFunctionName_ByCommand_ReturnsReceiveSectionOnly()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetHandlingFunctionName("LOTSTART");

            Assert.That(rtn.rc, Is.EqualTo(1));
            Assert.That(rtn.res, Is.EqualTo(LotStart));

            // LOTSTART_ACK belongs to a ReplyMsg section so must not be matched
            Assert.That(cfg.GetHandlingFunctionName("LOTSTART_ACK").rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task GetHandlingFunctionName_ByFrames_MatchesWhenEachFrameContainsSetting()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetHandlingFunctionName(new[] { "LOTSTART" }), Is.EqualTo((1, LotStart)));
            Assert.That(cfg.GetHandlingFunctionName(new[] { "LOTSTART", "xxA,B,Cxx" }), Is.EqualTo((1, LotStart)));
        }

        [Test]
        public async Task GetHandlingFunctionName_ByFrames_NoMatch_ReturnsMinus2()
        {
            var cfg = await LoadValidAsync();

            var (rc, _) = cfg.GetHandlingFunctionName(new[] { "LOTEND" });

            Assert.That(rc, Is.EqualTo(-2));
        }

        /// <summary>
        /// KNOWN BUG: when more frames are received than FrameData entries configured, and all
        /// configured frames match, the lookup indexes past cfg.FrameData and the exception is
        /// reported as rc=-1, which also stops it checking any remaining sections.
        /// Expected behaviour is a normal "not found" (-2) / skip to the next section.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public async Task GetHandlingFunctionName_ByFrames_MoreFramesThanConfigured_ShouldNotThrowInternally()
        {
            var cfg = await LoadValidAsync();

            var (rc, res) = cfg.GetHandlingFunctionName(new[] { "LOTSTART", "A,B,C", "EXTRA" });

            Assert.That(rc, Is.EqualTo(-2), res);
        }

        [Test]
        public async Task GetReplyFunctionName_KnownAndUnknownFunction()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetReplyFunctionName(LotStart);
            Assert.That(rtn.rc, Is.EqualTo(1));
            Assert.That(rtn.res, Is.EqualTo(LotStartReply));

            Assert.That(cfg.GetReplyFunctionName("NoSuchCmd").rc, Is.EqualTo(-2));
        }

        [Test]
        public async Task GetReplyEventNames_ReplyWithEventSection_ReturnsList()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetReplyEventNames(AlarmEvent);

            Assert.That(rtn.rc, Is.EqualTo(new[] { "AlarmSet", "AlarmClear" }));
            Assert.That(rtn.res, Is.Empty);
        }

        [Test]
        public async Task GetReplyEventNames_UnknownFunction_ReturnsNullList()
        {
            var cfg = await LoadValidAsync();

            var rtn = cfg.GetReplyEventNames("NoSuchCmd");

            Assert.That(rtn.rc, Is.Null);
            Assert.That(rtn.res, Does.Contain("NoSuchCmd"));
        }

        [Test]
        public async Task GetReplyEventName_SingleEventNameIsNeverPopulated()
        {
            // Only the numbered EventNameN keys are read, so EventName stays null.
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetReplyEventName(AlarmEvent), Is.Null);
            Assert.Throws<ApplicationException>(() => cfg.GetReplyEventName("NoSuchCmd"));
        }

        #endregion

        #region Sub-section lookups

        [Test]
        public async Task GetMsgSubSectionName_ByFunctionNumber()
        {
            var cfg = await LoadValidAsync();

            var found = cfg.GetMsgSubSectionName(LotStart, 1);
            Assert.That(found.rc, Is.EqualTo(1));
            Assert.That(found.res, Is.EqualTo("LotStartF1Cmd"));

            Assert.That(cfg.GetMsgSubSectionName("NoSuchCmd", 1).rc, Is.EqualTo(-2), "unknown function");
            Assert.That(cfg.GetMsgSubSectionName(LotStartReply, 1).rc, Is.EqualTo(-3), "no function sections");
            Assert.That(cfg.GetMsgSubSectionName(LotStart, 9).rc, Is.EqualTo(-4), "unknown function number");
            Assert.That(cfg.GetMsgSubSectionName(LotStart, 2).rc, Is.EqualTo(-5), "empty section name");
        }

        [Test]
        public async Task GetMsgSubSectionName_BySubCommand()
        {
            var cfg = await LoadValidAsync();

            var found = cfg.GetMsgSubSectionName(LotStart, "RECIPE");
            Assert.That(found.rc, Is.EqualTo(1));
            Assert.That(found.res, Is.EqualTo("LotStartRecipeCmd"));

            Assert.That(cfg.GetMsgSubSectionName("NoSuchCmd", "RECIPE").rc, Is.EqualTo(-2));
            Assert.That(cfg.GetMsgSubSectionName(LotStartReply, "RECIPE").rc, Is.EqualTo(-3));
            Assert.That(cfg.GetMsgSubSectionName(LotStart, "UNKNOWN").rc, Is.EqualTo(-4));
            Assert.That(cfg.GetMsgSubSectionName(LotStart, "EMPTY").rc, Is.EqualTo(-5));
        }

        [Test]
        public async Task GetMsgSubSectionName_ByKeyNameThenSubCommand_PrefersKeyName()
        {
            var cfg = await LoadValidAsync();

            var byKey = cfg.GetMsgSubSectionName(LotStart, "RCP_KEY", "RECIPE");
            Assert.That(byKey.res, Is.EqualTo("LotStartRcpKeyCmd"));

            var fallback = cfg.GetMsgSubSectionName(LotStart, "UNKNOWN_KEY", "RECIPE");
            Assert.That(fallback.rc, Is.EqualTo(1));
            Assert.That(fallback.res, Is.EqualTo("LotStartRecipeCmd"));

            Assert.That(cfg.GetMsgSubSectionName(LotStart, "X", "Y").rc, Is.EqualTo(-4));
            Assert.That(cfg.GetMsgSubSectionName(LotStart, "EMPTY", "RECIPE").rc, Is.EqualTo(-5),
                "an empty key-name match is reported, not skipped");
            Assert.That(cfg.GetMsgSubSectionName(LotStartReply, "X", "Y").rc, Is.EqualTo(-3));
            Assert.That(cfg.GetMsgSubSectionName("NoSuchCmd", "X", "Y").rc, Is.EqualTo(-2));
        }

        #endregion

        #region GetMsgHandlingCommonSettings

        [Test]
        public async Task GetMsgHandlingCommonSettings_KnownFunction_FillsSettings()
        {
            var cfg = await LoadValidAsync();
            IMsgHandlingCommonSettings settings = new CommonHandlingMethodSettings();

            cfg.GetMsgHandlingCommonSettings(LotStart, ref settings);

            Assert.That(settings.rc, Is.EqualTo(1));
            Assert.That(settings.result, Is.Empty);
            Assert.That(settings.MinFrameCount, Is.EqualTo(2));
            Assert.That(settings.FrameDelim, Is.EqualTo("|"));
            Assert.That(settings.NestedFrameDelim, Is.EqualTo("^"));
        }

        [Test]
        public async Task GetMsgHandlingCommonSettings_UnknownFunction_CallsFailed()
        {
            var cfg = await LoadValidAsync();
            IMsgHandlingCommonSettings settings = new CommonHandlingMethodSettings
            {
                MinFrameCount = 7, FrameDelim = "x", NestedFrameDelim = "y"
            };

            cfg.GetMsgHandlingCommonSettings("NoSuchCmd", ref settings);

            Assert.That(settings.rc, Is.EqualTo(-2));
            Assert.That(settings.result, Does.Contain("NoSuchCmd"));
            Assert.That(settings.MinFrameCount, Is.EqualTo(0));
            Assert.That(settings.FrameDelim, Is.Empty);
            Assert.That(settings.NestedFrameDelim, Is.Empty);
        }

        #endregion
    }
}
