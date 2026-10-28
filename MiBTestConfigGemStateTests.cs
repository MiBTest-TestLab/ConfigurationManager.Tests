using System.Threading.Tasks;
using ConfigurationManager.Tests.Helpers;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for the Link / Control / Spool state enum sections and their lookup methods.
    /// </summary>
    [TestFixture]
    public class MiBTestConfigGemStateTests : MiBTestConfigTestBase
    {
        [Test]
        public async Task GemStateEnums_ValidSections_ArePopulated()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.LinkStateEnum, Is.EquivalentTo(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, int>("Disabled", 0),
                new System.Collections.Generic.KeyValuePair<string, int>("Enabled", 1),
            }));
            Assert.That(cfg.ControlStateEnum["OnlineRemote"], Is.EqualTo(5));
            Assert.That(cfg.SpoolStateEnum["Active"], Is.EqualTo(1));
        }

        [TestCase("LinkStateEnum")]
        [TestCase("ControlStateEnum")]
        [TestCase("SpoolStateEnum")]
        public async Task GemStateEnum_SectionMissing_ReturnsMinus2(string section)
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().RemoveSection(section);

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-2));
            Assert.That(res, Does.Contain(section).And.Contain("missing"));
        }

        [Test]
        public async Task GemStateEnum_NonNumericValue_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().Set("ControlStateEnum", "EqOffline", "one");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3));
            Assert.That(res, Does.Contain("EqOffline").And.Contain("not numeric"));
        }

        /// <summary>
        /// KNOWN BUG: ExtractGemStateEnum finds the bad value with SingleOrDefault, which throws
        /// when two or more values are non-numeric, so the result is -1 instead of -3.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public async Task GemStateEnum_TwoNonNumericValues_ReturnsMinus3()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig()
                .Set("ControlStateEnum", "EqOffline", "one")
                .Set("ControlStateEnum", "OnlineRemote", "five");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-3), res);
        }

        [Test]
        public async Task GemStateEnum_EmptySection_ReturnsMinus4()
        {
            var ini = MiBTestConfigFixture.ValidSocketCommConfig().ClearSection("SpoolStateEnum");

            var (_, rc, res) = await LoadAsync(ini);

            Assert.That(rc, Is.EqualTo(-4));
            Assert.That(res, Does.Contain("SpoolStateEnum").And.Contain("empty"));
        }

        [Test]
        public async Task GetStateNumber_KnownStates_ReturnStateNumber()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetLinkStateNumber("Enabled"), Is.EqualTo((1, string.Empty)));
            Assert.That(cfg.GetControlStateNumber("OnlineRemote"), Is.EqualTo((5, string.Empty)));
            Assert.That(cfg.GetSpoolStateNumber("Inactive"), Is.EqualTo((0, string.Empty)));
        }

        [Test]
        public async Task GetStateNumber_UnknownState_ReturnsMinus1()
        {
            var cfg = await LoadValidAsync();

            Assert.That(cfg.GetLinkStateNumber("Bogus").Item1, Is.EqualTo(-1));
            Assert.That(cfg.GetControlStateNumber("Bogus").Item1, Is.EqualTo(-1));
            Assert.That(cfg.GetSpoolStateNumber("Bogus").Item1, Is.EqualTo(-1));
        }

        [Test]
        public void GetStateNumber_BeforeRead_ReturnsMinus1()
        {
            var cfg = CreateConfig(MiBTestConfigFixture.ValidSocketCommConfig());

            var (rc, res) = cfg.GetLinkStateNumber("Enabled");

            Assert.That(rc, Is.EqualTo(-1));
            Assert.That(res, Does.Contain("Enabled"));
        }

        [TestCase("Link", "Enabled", 1)]
        [TestCase("Control", "OnlineRemote", 5)]
        [TestCase("Spool", "Active", 1)]
        public async Task GetGemStateNumber_ModelNameMatchesField_ReturnsStateNumber(string model, string state, int expected)
        {
            var cfg = await LoadValidAsync();

            var (rc, res) = cfg.GetGemStateNumber(model, state);

            Assert.That(rc, Is.EqualTo(expected), res);
            Assert.That(res, Is.Empty);
        }

        /// <summary>
        /// KNOWN BUG: GetGemStateNumber uses First(), which throws when no field matches, so the
        /// "== null -> -2" branch is dead code and an unknown model returns -1.
        /// </summary>
        [Test]
        [Category("KnownBug")]
        public async Task GetGemStateNumber_UnknownModel_ReturnsMinus2()
        {
            var cfg = await LoadValidAsync();

            var (rc, res) = cfg.GetGemStateNumber("Process", "Idle");

            Assert.That(rc, Is.EqualTo(-2), res);   // today: -1, because First() throws
        }

        [Test]
        public async Task GetGemStateNumber_UnknownState_ReturnsNegativeCode()
        {
            var cfg = await LoadValidAsync();

            var (rc, _) = cfg.GetGemStateNumber("Control", "Bogus");

            Assert.That(rc, Is.LessThan(0));
        }
    }
}
