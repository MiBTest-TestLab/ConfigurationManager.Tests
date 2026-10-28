using System.Collections.Generic;
using NUnit.Framework;

namespace ConfigurationManager.Tests
{
    /// <summary>
    /// Tests for the small settings / result classes that contain real logic
    /// (constructors with conversion, ToString formatting, Failed() reset).
    /// </summary>
    [TestFixture]
    public class ModelTests
    {
        #region MapSetupDataFuncArgs

        [Test]
        public void MapSetupDataFuncArgs_KnownType_ResolvesTypeAndIndices()
        {
            var arg = new MapSetupDataFuncArgs("Rows", "System.Int32", 3, 7, "ToRows");

            Assert.That(arg.Name, Is.EqualTo("Rows"));
            Assert.That(arg.Type, Is.EqualTo(typeof(int)));
            Assert.That(arg.RowIndices.Start, Is.EqualTo(3));
            Assert.That(arg.RowIndices.End, Is.EqualTo(7));
            Assert.That(arg.ConvertFuncName, Is.EqualTo("ToRows"));
        }

        [Test]
        public void MapSetupDataFuncArgs_DefaultConvertFunction_IsNone()
        {
            var arg = new MapSetupDataFuncArgs("Id", "System.String", 1, 1);

            Assert.That(arg.ConvertFuncName, Is.EqualTo("None"));
        }

        [TestCase("Foo.Bar")]
        [TestCase("int")] // C# alias, not a CLR type name
        public void MapSetupDataFuncArgs_UnknownType_LeavesTypeNull(string typeName)
        {
            var arg = new MapSetupDataFuncArgs("X", typeName, 1, 1);

            Assert.That(arg.Type, Is.Null);
        }

        #endregion

        #region MapSetupParamsBase

        [Test]
        public void MapSetupParamsBase_ToString_NoParams()
        {
            var settings = new MapSetupParamsBase("MapSetupDataSendParams", @"C:\MiBTest.ini");

            string text = settings.ToString();

            Assert.That(text, Does.Contain(@"MapSetupDataSendParams Settings from file: C:\MiBTest.ini"));
            Assert.That(text, Does.Contain("No settings were found!"));
        }

        [Test]
        public void MapSetupParamsBase_ToString_ListsEveryParam()
        {
            var settings = new MapSetupParamsBase("Sec", "file.ini")
            {
                Params = new List<IFunctionArgumentInfo>
                {
                    new MapSetupDataFuncArgs("Id", "System.String", 1, 2),
                    new MapSetupDataFuncArgs("Rows", "System.Int32", 3, 4, "ToRows"),
                }
            };

            string text = settings.ToString();

            Assert.That(text, Does.Contain("Name=Id, Type=System.String, RowStart=1, RowEnd=2, ConvFnName=None"));
            Assert.That(text, Does.Contain("Name=Rows, Type=System.Int32, RowStart=3, RowEnd=4, ConvFnName=ToRows"));
        }

        #endregion

        #region HandlingMethodCfg

        [Test]
        public void HandlingMethodCfg_ToString_FullSettings()
        {
            var cfg = new HandlingMethodCfg
            {
                MethodName = "LotStartCmd",
                CommType = CommTypes.ReceiveMsg,
                FrameDelimiter = "|",
                NestedDelimiter = "^",
                FrameCount = 2,
                VariableFrames = true,
                FrameData = new List<string> { "LOTSTART", "A,B" },
                ReplySecName = "LotStartReplyCmd",
                FuncSections = new Dictionary<int, string> { { 1, "F1Cmd" } },
                SubCmdSections = new Dictionary<string, string> { { "RECIPE", "RecipeCmd" } },
            };

            string text = cfg.ToString();

            Assert.That(text, Does.Contain("Handling method name=LotStartCmd"));
            Assert.That(text, Does.Contain("CommType=ReceiveMsg"));
            Assert.That(text, Does.Contain("FrameCount=2"));
            Assert.That(text, Does.Contain("VariableFrames=True"));
            Assert.That(text, Does.Contain("Reply=LotStartReplyCmd"));
            Assert.That(text, Does.Contain("Frame Data 2=A,B"));
            Assert.That(text, Does.Contain("Function 1 section name: F1Cmd"));
            Assert.That(text, Does.Contain("Sub-Command: RECIPE, section name: RecipeCmd"));
        }

        [Test]
        public void HandlingMethodCfg_ToString_EventNamesAndNoFrameData()
        {
            var cfg = new HandlingMethodCfg
            {
                MethodName = "AlarmEventCmd",
                CommType = CommTypes.ReplyWithEvent,
                EventNames = new List<string> { "AlarmSet", "AlarmClear" },
                // FuncSections are only printed after frame data, so they are skipped here
                FuncSections = new Dictionary<int, string> { { 1, "F1Cmd" } },
            };

            string text = cfg.ToString();

            Assert.That(text, Does.Contain("EventName1=AlarmSet"));
            Assert.That(text, Does.Contain("EventName2=AlarmClear"));
            Assert.That(text, Does.Contain("No FrameData settings found"));
            Assert.That(text, Does.Not.Contain("F1Cmd"));
        }

        [Test]
        public void HandlingMethodCfg_ToString_EmptyFrameData()
        {
            var cfg = new HandlingMethodCfg { MethodName = "X", FrameData = new List<string>() };

            Assert.That(cfg.ToString(), Does.Contain("No FrameData settings found"));
        }

        #endregion

        #region CommonHandlingMethodSettings

        [Test]
        public void CommonHandlingMethodSettings_Failed_SetsErrorAndResetsSettings()
        {
            var settings = new CommonHandlingMethodSettings
            {
                rc = 1, result = "", FrameDelim = "|", NestedFrameDelim = "^", MinFrameCount = 3
            };

            settings.Failed(-7, "boom");

            Assert.That(settings.rc, Is.EqualTo(-7));
            Assert.That(settings.result, Is.EqualTo("boom"));
            Assert.That(settings.FrameDelim, Is.Empty);
            Assert.That(settings.NestedFrameDelim, Is.Empty);
            Assert.That(settings.MinFrameCount, Is.EqualTo(0));
        }

        #endregion

        #region Simple value types

        [Test]
        public void GemCliVariable_ConstructorAndToString()
        {
            var v = new GemCliVariable("Sec", "Var", "Bool", "OnChanged");

            Assert.That(v.SecName, Is.EqualTo("Sec"));
            Assert.That(v.VarName, Is.EqualTo("Var"));
            Assert.That(v.VarType, Is.EqualTo("Bool"));
            Assert.That(v.PropertyChangedFnName, Is.EqualTo("OnChanged"));
            Assert.That(v.ToString(),
                Is.EqualTo("CLI Variable: Section: Sec, Name: Var, Type: Bool, PropChangeFn: OnChanged"));
        }

        [Test]
        public void CLIVarCfg_ToString()
        {
            var v = new CLIVarCfg { Container = "C", Section = "S", Variable = "V" };

            Assert.That(v.ToString(), Is.EqualTo("Container: C, Section: S, Variable: V"));
        }

        [Test]
        public void IntReturn_Constructor_SetsCodeAndResult()
        {
            var r = new IntReturn(-3, "err");

            Assert.That(r.rc, Is.EqualTo(-3));
            Assert.That(r.res, Is.EqualTo("err"));
        }

        [Test]
        public void StringListReturn_DefaultResultIsEmpty()
        {
            var list = new List<string> { "a" };
            var r = new StringListReturn(list);

            Assert.That(r.rc, Is.SameAs(list));
            Assert.That(r.res, Is.Empty);
        }

        #endregion
    }
}
