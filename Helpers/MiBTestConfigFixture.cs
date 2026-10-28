namespace ConfigurationManager.Tests.Helpers
{
    /// <summary>
    /// Builds a complete, valid Socket Comm MiBTest.ini so that individual tests only need to
    /// break the one setting they are interested in.
    /// </summary>
    public static class MiBTestConfigFixture
    {
        public const string Dll = "DLL";

        public static IniBuilder ValidSocketCommConfig()
        {
            return new IniBuilder()
                .Set(Dll, "SocketCommHandlerDLLFilePath", @" C:\Handlers\SocComm.dll ")
                .Set(Dll, "SocketCommHandlerClassName", "SocComm.Handler")
                .Set(Dll, "MiBDllFilePath", @"C:\MiB\MiB.dll")
                .Set(Dll, "MiBClassName", "MiB.Main")
                .Set(Dll, "PPDirPath", @"C:\PP")
                .Set(Dll, "DSDirPath", @"C:\DS")
                .Set(Dll, "BinaryRecipeFormat", "0")
                .Set(Dll, "StiGemConfigFilePath", @"C:\StiGem\Custom.cfg")

                .Set("LinkStateEnum", "Disabled", "0")
                .Set("LinkStateEnum", "Enabled", "1")
                .Set("ControlStateEnum", "EqOffline", "1")
                .Set("ControlStateEnum", "OnlineRemote", "5")
                .Set("SpoolStateEnum", "Inactive", "0")
                .Set("SpoolStateEnum", "Active", "1")

                .Set("SECSDataTypeEnum", "1", "A")
                .Set("SECSDataTypeEnum", "2", "U4")

                .Set("RemoteCommandHandlers", "START", "StartCmd")
                .Set("RemoteCommandHandlers", "STOP", "StopCmd")

                .Set("WaferMapSetupFileItems", "WaferSize", "WAFER_SIZE , U4")
                .Set("WaferMapSetupFileItems", "FlatOrientation", "FLAT,U2")

                .Set("MapSetupDataRequestParams", "WaferId", "System.String,1,1")
                .Set("MapSetupDataRequestParams", "RowCount", "System.Int32, 2, 3, ToRowCount")
                .Set("MapSetupDataSendParams", "WaferId", "System.String,1,1")
                .Set("MapTransmitInquireParams", "MaterialId", "System.String,1,1")
                .Set("MapDataType2SendParams", "MaterialId", "System.String,1,1")

                // Receive message: hex frame delimiter, function + sub-command sub-sections
                .Set("LotStartCmd", "CommType", "1")
                .Set("LotStartCmd", "FrameDelimiter", "0x7C")
                .Set("LotStartCmd", "NestedFrameDelimiter", "^")
                .Set("LotStartCmd", "MinFrameCount", "2")
                .Set("LotStartCmd", "VariableFrames", "0")
                .Set("LotStartCmd", "FrameData1", "LOTSTART")
                .Set("LotStartCmd", "FrameData2", "A,B,C")
                .Set("LotStartCmd", "Reply", "LotStartReplyCmd")
                .Set("LotStartCmd", "Function1_Section", "LotStartF1Cmd")
                .Set("LotStartCmd", "Function2_Section", "")
                .Set("LotStartCmd", "RECIPE_Section", "LotStartRecipeCmd")
                .Set("LotStartCmd", "RCP_KEY_Section", "LotStartRcpKeyCmd")
                .Set("LotStartCmd", "EMPTY_Section", "")

                // Reply message: plain delimiter, no nested delimiter, no Reply key
                .Set("LotStartReplyCmd", "CommType", "3")
                .Set("LotStartReplyCmd", "FrameDelimiter", "|")
                .Set("LotStartReplyCmd", "MinFrameCount", "1")
                .Set("LotStartReplyCmd", "VariableFrames", "1")
                .Set("LotStartReplyCmd", "FrameData1", "LOTSTART_ACK")

                // Reply-with-event message: only event names are read
                .Set("AlarmEventCmd", "CommType", "4")
                .Set("AlarmEventCmd", "EventName1", " AlarmSet ")
                .Set("AlarmEventCmd", "EventName2", "AlarmClear");
        }
    }
}
