using IniParser.Model;
using IniParser.Parser;

namespace ConfigurationManager.Tests.Helpers
{
    /// <summary>
    /// Writes a complete, valid set of STI CLI SECS/GEM configuration files into a
    /// <see cref="TempDirectory"/> and exposes the main [STICLI SECSGEM] section as an
    /// <see cref="IniBuilder"/> so tests can break individual settings.
    /// </summary>
    public class GemCliConfigFixture
    {
        public const string MainSection = "STICLI SECSGEM";

        /// <summary>
        /// All CLI variable key names GemCliConfig requires (mirrors GemCliConfig.GemCliVarKeynames).
        /// </summary>
        public static readonly string[] CliVarKeys =
        {
            "MiBStart", "ShowWindow", "GetVersionInfo", "MiBTestWindowHandle", "CommState",
            "ControlState", "SpoolState", "EnableComm", "GoOffline", "GoLocal", "EnableSpool",
            "PurgeSpool", "EventName", "SendEvent", "AlarmID", "SetAlarm", "ClearAlarm",
            "SetVariableName", "VIDSet", "GetVariableName", "VIDGet", "RCMDFileName", "RCMDSent",
            "DownloadRecipeFileName", "HostDownload", "EqptDownload", "UploadRecipeFileName",
            "HostUpload", "EqptUpload", "RecipeDirRequest", "TermReqFileName", "TermReqSent",
            "ObjType", "AttributeDataFile", "GetAttributes",
        };

        public const string ValidJson = @"{
  ""CliGemVarConfig"": [
    { ""VarName"": ""LotId"", ""VID"": 1001, ""Data"": { ""Type"": ""A"", ""Value"": ""GEM/Lot/LotId"" } }
  ]
}";

        private readonly TempDirectory dir;

        public IniBuilder Main { get; }
        public IniBuilder VariableCfg { get; set; }
        public IniBuilder RcmdCfg { get; set; }
        public IniBuilder TermReqCfg { get; set; }
        public IniBuilder AttrReqCfg { get; set; }
        public IniBuilder AttrReqMsgCfg { get; set; }
        public string Json { get; set; } = ValidJson;

        public GemCliConfigFixture(TempDirectory dir)
        {
            this.dir = dir;

            Main = new IniBuilder()
                .Set(MainSection, "CLIPath", @" C:\CLI\Cache ")
                .Set(MainSection, "EventConfig", $"{dir.PathOf("events.ini")},Events")
                .Set(MainSection, "VariableConfig", $"{dir.PathOf("vars.ini")},GemVar")
                .Set(MainSection, "RemoteCommandConfig", $"{dir.PathOf("rcmd.ini")},Rcmd")
                .Set(MainSection, "TerminalRequestConfig", $"{dir.PathOf("termreq.ini")},TermReq")
                .Set(MainSection, "AttributeRequestConfig", $"{dir.PathOf("attrreq.ini")},AttrReq")
                .Set(MainSection, "CLIMmiWinHandle", " MMI / Window / Handle ")
                .Set(MainSection, "Container", " SECSGEM ");
            foreach (string key in CliVarKeys)
                Main.Set(MainSection, key, $"Sec_{key}, Var_{key}, String");
            // One variable with the optional property-changed function name
            Main.Set(MainSection, "SendEvent", "Events, SendEvent, Bool, OnSendEvent");

            VariableCfg = new IniBuilder()
                .Set("GemVar", "JsonFilePath", dir.PathOf("vids.json"))
                .Set("GemVar", "VariableGetFolder", @"C:\VarGet");

            RcmdCfg = new IniBuilder().Set("Rcmd", "RCMDDestPath", @"C:\RCMD, txt");

            TermReqCfg = new IniBuilder().Set("TermReq", "TerminalReqDestPath", @"C:\Term\Out\TermReq.dat");

            AttrReqCfg = new IniBuilder()
                .Set("AttrReq", "AttributRequestConfigPath", dir.PathOf("attrmsg.ini"))
                .Set("AttrReq", "AttributeDataFolder", @"C:\AttrData");

            AttrReqMsgCfg = new IniBuilder()
                .Set("Carrier", "OBJSPEC", "EQ")
                .Set("Carrier", "OBJTYPE", "Carrier")
                .Set("Carrier", "ObjIDs", "C1,C2");
        }

        /// <summary>
        /// Writes every file (except those the test explicitly nulls out) and returns a
        /// GemCliConfig constructed from the main section.
        /// </summary>
        public GemCliConfig Build()
        {
            if (VariableCfg != null) dir.WriteFile("vars.ini", VariableCfg.ToString());
            if (Json != null) dir.WriteFile("vids.json", Json);
            if (RcmdCfg != null) dir.WriteFile("rcmd.ini", RcmdCfg.ToString());
            if (TermReqCfg != null) dir.WriteFile("termreq.ini", TermReqCfg.ToString());
            if (AttrReqCfg != null) dir.WriteFile("attrreq.ini", AttrReqCfg.ToString());
            if (AttrReqMsgCfg != null) dir.WriteFile("attrmsg.ini", AttrReqMsgCfg.ToString());

            return new GemCliConfig(Parse(Main));
        }

        public static IniData Parse(IniBuilder ini) => new IniDataParser().Parse(ini.ToString());
    }
}
