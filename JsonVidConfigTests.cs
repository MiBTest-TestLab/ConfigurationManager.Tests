using System;
using System.Collections.Generic;
using JsonVidSettings;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for the JsonVidSettings namespace: <see cref="JsonVidConfig"/>,
    /// <see cref="CliGemVarCfgJsonDeserializer"/> and the custom <see cref="DataJsonConverter"/>.
    /// </summary>
    [TestFixture]
    public class JsonVidConfigTests
    {
        private const string Json = @"{
  ""CliGemVarConfig"": [
    {
      ""VarName"": ""LotId"",
      ""VID"": 1001,
      ""Data"": { ""Type"": ""A"", ""Value"": ""GEM/Lot/LotId"" }
    },
    {
      ""VarName"": ""WaferList"",
      ""VID"": 1002,
      ""Poll"": true,
      ""Data"": {
        ""Type"": ""L"",
        ""Value"": [
          { ""Type"": ""A"", ""Value"": ""GEM/Wafer/Id"" },
          { ""Type"": ""L"", ""Value"": [ { ""Type"": ""U4"", ""Value"": ""GEM/Wafer/Count"" } ] }
        ]
      }
    }
  ]
}";

        private static JsonVidConfig Deserialized()
        {
            var cfg = new JsonVidConfig(Json);
            cfg.Deserialize();
            return cfg;
        }

        #region Deserialize

        [Test]
        public void Deserialize_ValidJson_KeysSettingsByVarName()
        {
            var cfg = Deserialized();

            Assert.That(cfg.JsonCfg, Is.EqualTo(Json));
            Assert.That(cfg.JsonGemVarCfg.Keys, Is.EquivalentTo(new[] { "LotId", "WaferList" }));
        }

        [Test]
        public void Deserialize_StringValue_BecomesStringData()
        {
            var lotId = Deserialized().JsonGemVarCfg["LotId"];

            Assert.That(lotId.VID, Is.EqualTo(1001));
            Assert.That(lotId.Poll, Is.False, "Poll defaults to false when omitted");
            Assert.That(lotId.Data, Is.TypeOf<StringData>());
            Assert.That(lotId.Data.Type, Is.EqualTo("A"));
            Assert.That(((StringData)lotId.Data).Value, Is.EqualTo("GEM/Lot/LotId"));
        }

        [Test]
        public void Deserialize_ArrayValue_BecomesNestedListData()
        {
            var waferList = Deserialized().JsonGemVarCfg["WaferList"];

            Assert.That(waferList.Poll, Is.True);
            var list = waferList.Data as ListData;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.Type, Is.EqualTo("L"));
            Assert.That(list.Value, Has.Count.EqualTo(2));
            Assert.That(list.Value[0], Is.TypeOf<StringData>());

            var inner = list.Value[1] as ListData;
            Assert.That(inner, Is.Not.Null);
            Assert.That(((StringData)inner.Value[0]).Value, Is.EqualTo("GEM/Wafer/Count"));
            Assert.That(inner.Value[0].Type, Is.EqualTo("U4"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Deserialize_NullOrEmpty_Throws(string json)
        {
            var cfg = new JsonVidConfig(json);

            var ex = Assert.Throws<JsonVidSettingsException>(() => cfg.Deserialize());
            Assert.That(ex.InnerException, Is.TypeOf<JsonVidSettingsException>());
            Assert.That(ex.InnerException.Message, Does.Contain("empty or null"));
        }

        [TestCase("null")]
        [TestCase("{ not json")]
        [TestCase(@"{ ""CliGemVarConfig"": [ { ""VarName"": ""X"", ""VID"": 1, ""Data"": { ""Type"": ""U4"", ""Value"": 5 } } ] }")]
        public void Deserialize_InvalidContent_Throws(string json)
        {
            var cfg = new JsonVidConfig(json);

            Assert.Throws<JsonVidSettingsException>(() => cfg.Deserialize());
        }

        [Test]
        public void Deserialize_DuplicateVarName_Throws()
        {
            const string dup = @"{ ""CliGemVarConfig"": [
                { ""VarName"": ""X"", ""VID"": 1, ""Data"": { ""Type"": ""A"", ""Value"": ""a"" } },
                { ""VarName"": ""X"", ""VID"": 2, ""Data"": { ""Type"": ""A"", ""Value"": ""b"" } } ] }";

            Assert.Throws<JsonVidSettingsException>(() => new JsonVidConfig(dup).Deserialize());
        }

        [Test]
        public void Deserializer_ReturnsRootWithAllEntries()
        {
            Root root = CliGemVarCfgJsonDeserializer.Deserialize(Json);

            Assert.That(root.CliGemVarConfig, Has.Count.EqualTo(2));
        }

        [Test]
        public void DataJsonConverter_WriteJson_IsNotSupported()
        {
            Assert.Throws<NotImplementedException>(() =>
                Newtonsoft.Json.JsonConvert.SerializeObject(new StringData { Type = "A", Value = "v" },
                    new DataJsonConverter()));
        }

        #endregion

        #region GetCliGemVarSetting

        [Test]
        public void GetCliGemVarSetting_ByName_ReturnsSetting()
        {
            Assert.That(Deserialized().GetCliGemVarSetting("WaferList").VID, Is.EqualTo(1002));
        }

        [Test]
        public void GetCliGemVarSetting_ByUnknownName_Throws()
        {
            var ex = Assert.Throws<JsonVidSettingsException>(() => Deserialized().GetCliGemVarSetting("Nope"));
            Assert.That(ex.InnerException.Message, Does.Contain("Nope"));
        }

        [Test]
        public void GetCliGemVarSetting_ByVid_ReturnsSetting()
        {
            Assert.That(Deserialized().GetCliGemVarSetting(1001).VarName, Is.EqualTo("LotId"));
        }

        [Test]
        public void GetCliGemVarSetting_ByUnknownVid_Throws()
        {
            Assert.Throws<JsonVidSettingsException>(() => Deserialized().GetCliGemVarSetting(9999));
        }

        [Test]
        public void GetCliGemVarSetting_ByVids_SkipsUnknownVids()
        {
            // Unknown VIDs may be Hume built-in VIDs, so they are skipped rather than reported.
            Dictionary<int, JsonCliGemVarConfig> settings =
                Deserialized().GetCliGemVarSetting(new[] { 1002, 9999, 1001 });

            Assert.That(settings.Keys, Is.EquivalentTo(new[] { 1001, 1002 }));
            Assert.That(settings[1002].VarName, Is.EqualTo("WaferList"));
        }

        [Test]
        public void GetCliGemVarSetting_BeforeDeserialize_Throws()
        {
            var cfg = new JsonVidConfig(Json);

            Assert.Throws<JsonVidSettingsException>(() => cfg.GetCliGemVarSetting("LotId"));
            Assert.Throws<JsonVidSettingsException>(() => cfg.GetCliGemVarSetting(1001));
            Assert.Throws<JsonVidSettingsException>(() => cfg.GetCliGemVarSetting(new[] { 1001 }));
        }

        #endregion

        #region ToString

        [Test]
        public void ToString_BeforeDeserialize_ReturnsEmpty()
        {
            Assert.That(new JsonVidConfig(Json).ToString(), Is.Empty);
        }

        [Test]
        public void ToString_AfterDeserialize_ListsEveryVariable()
        {
            string text = Deserialized().ToString();

            Assert.That(text, Does.Contain("VarName:LotId, VID:1001, Type:A"));
            Assert.That(text, Does.Contain("VarName:WaferList, VID:1002, Type:L"));
            Assert.That(text, Does.Contain("GEM/Wafer/Count"));
        }

        [Test]
        public void StringData_ToString_ShowsTypeAndValue()
        {
            Assert.That(new StringData { Type = "A", Value = "x" }.ToString(), Is.EqualTo("Type: A, Value:x"));
        }

        #endregion
    }
}
