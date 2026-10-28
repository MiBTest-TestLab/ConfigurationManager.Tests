# ConfigurationManager.Tests

Unit tests for `ConfigurationManager`, the library that reads the MiBTest INI configuration (`MiBTest.ini`), the STI CLI SECS/GEM configuration files and the CLI GEM Variable JSON file.

## Test Environment & Dependencies

| Item             | Details                                |
| ---------------- | -------------------------------------- |
| Test Project     | `ConfigurationManager.Tests`           |
| Target Framework | .NET Framework 4.8                     |
| IDE              | Visual Studio 2022                     |
| Language Version | `latest` (see note below)              |
| Solution         | `CSMiBTest-LowRes\MiBTest.sln` → `Tests` folder |

### Project References

* `..\ConfigurationManager\ConfigurationManager.csproj` – production code under test.

### Assembly References

| Assembly        | Path                                                        | Why                                                          |
| --------------- | ----------------------------------------------------------- | ------------------------------------------------------------ |
| `Result`        | `..\SharedClasses\Result\bin\Debug\Result.dll`              | `IResult<int, string>` is returned by most public methods    |
| `INIFileParser` | `..\CSMiBTest-LowRes\packages\ini-parser.2.5.2\...`         | Build `IniData` for `GemCliConfig`, which takes it in its constructor |
| `Newtonsoft.Json` | `..\CSMiBTest-LowRes\packages\Newtonsoft.Json.13.0.3\...` | Exercise `DataJsonConverter` directly                        |

> `Result.dll` must be built first (`SharedClasses\Result\Result.sln`), same as for `ConfigurationManager` itself.

### NuGet Packages

| Package           | Version | Purpose                                                                                   |
| ----------------- | ------: | ----------------------------------------------------------------------------------------- |
| NUnit             |   4.6.1 | Test framework                                                                            |
| NUnit3TestAdapter |   6.3.0 | Allows Visual Studio Test Explorer / `vstest.console.exe` to discover and run NUnit tests |
| Moq               | 4.20.72 | Available, not needed so far (everything under test is file driven)                       |

**`LangVersion=latest`** – NUnit 4.6 adds `Action` / `Func` overloads next to `TestDelegate` / `ActualValueDelegate` and relies on `[OverloadResolutionPriority]` to pick one. The default C# 7.3 for net48 ignores that attribute, so `Assert.Throws(() => ...)` and `Assert.That(() => ..., ...)` do not compile without it.

## Approach

All classes under test read their settings from files, and `MiBTestConfig` keeps the parsed INI data private. So the tests use **real temporary files** rather than mocks:

* `Helpers\TempDirectory` – unique folder under `%TEMP%` per test, deleted in `TearDown`.
* `Helpers\IniBuilder` – fluent builder for INI text (`Set`, `RemoveKey`, `RemoveSection`, `ClearSection`).
* `Helpers\MiBTestConfigFixture` – a complete, **valid** Socket Comm `MiBTest.ini`. Each test breaks exactly one setting and asserts the resulting error code / message.
* `Helpers\GemCliConfigFixture` – a complete, valid set of STI CLI files (main section, variable config, JSON, remote command, terminal request, attribute request and attribute message files). Set a file builder to `null` to simulate a missing file.

Golden-file tests complement the fixtures. They load frozen copies of real configuration files:

* `TestData\MiBTest.ini` – real Socket-mode configuration (`MiBTestConfigGoldenFileTests`).
* `TestData2\` – real STI CLI-mode set: `MiBTest.ini`, `CliGemVar 1.json`, `GetAttrReq.dat` (`MiBTestConfigStiCliGoldenFileTests`). The real file uses absolute `D:\tScanSecsGem\MiBTest\` paths, so each test copies the set into a temp folder and rewrites that prefix.

Fixture tests check the reading rules and every error code; golden-file tests check that the real configuration works. When a real configuration file changes, update its copy here and re-run the tests.

> A `;` used as the whole value (for example `FrameDelimiter=;`, as in the real MiBTest.ini) is read correctly; this is confirmed by `MiBTestConfigGoldenFileTests`. A `;` in the middle of a value has not been tested, so the fixture still avoids it.

## Test Scope

| Test class                            | Covers |
| ------------------------------------- | ------ |
| `MiBTestConfigDllSectionTests`        | Constructor, `ReadConfigFileAsync` flow, `[DLL]` section (every error code, defaults/warnings, `CommType` Socket vs STI CLI branch), `ToString` |
| `MiBTestConfigGemStateTests`          | `LinkStateEnum` / `ControlStateEnum` / `SpoolStateEnum` sections, `Get*StateNumber`, `GetGemStateNumber` |
| `MiBTestConfigSocketSettingsTests`    | `SECSDataTypeEnum`, `RemoteCommandHandlers`, `WaferMapSetupFileItems`, the four map setup parameter sections |
| `MiBTestConfigMsgHandlingTests`       | `*Cmd` message handling sections (CommType, hex delimiters, frame counts, frame data, function/sub-command sections) and every lookup method on top of them |
| `MiBTestConfigGoldenFileTests`        | Loads a frozen copy of the real `MiBTest.ini` (`TestData\MiBTest.ini`) end to end: `;` / `,` / hex delimiters, first-section-wins lookup, function sub-sections, real state names. Update the copy whenever the real file changes. |
| `MiBTestConfigStiCliGoldenFileTests`  | Loads a frozen copy of a real STI CLI-mode set (`TestData2\`: `MiBTest.ini`, `CliGemVar 1.json`, `GetAttrReq.dat`). Rewrites the absolute `D:\tScanSecsGem\MiBTest\` paths to a temp folder, then checks sub-sections inside MiBTest.ini, the 129-entry JSON (with a trailing comma), output locations, attribute requests and CLI variables. |
| `GemCliConfigTests`                   | `ReadGemCliConfig` success path, every error code, all referenced files, `GetCliVariable`, `ToString` |
| `SECSAttrReqCfgTests`                 | `SECSAttrReqCfg.ReadConfig`, `SECSAttrReqSetting` / `SECSAttrQualCfg` |
| `JsonVidConfigTests`                  | `JsonVidConfig`, `CliGemVarCfgJsonDeserializer`, `DataJsonConverter` (string / nested list data), all three `GetCliGemVarSetting` overloads |
| `ModelTests`                          | `MapSetupDataFuncArgs`, `MapSetupParamsBase`, `HandlingMethodCfg.ToString`, `CommonHandlingMethodSettings.Failed`, small value types |

### Deliberately Not Tested

* Interfaces (`IConfigManager`, `IMsgHandlingConfig`, ...) and pure property bags (`WaferMapItemsSettings`, `ValueRowIndices`).
* `JsonVidSettingsException` – only forwards to `Exception` constructors.
* `catch` blocks that only report unexpected exceptions (about 40 in `MiBTestConfig`).
* Dead code: `GemCliConfig.GetCliEventsToCEIDsCfg` (call disabled in ver 1.0.0.9), `MiBTestConfig.ExtractEventName`, `MiBTestConfig.Me`, and the private parameterless constructors of `GemCliConfig`, `SECSAttrReqCfg`, `JsonVidConfig` and `GemCliVariable`.
* Some `ToString()` branches (STI CLI mode, empty settings, nested lists).

Baseline coverage (2026-10-28): 85.1% line coverage of `ConfigurationManager`. Branch coverage is not collected by the Microsoft code coverage tool.

## Known Bugs (`[Category("KnownBug")]`)

These tests assert the **intended** behaviour and currently **fail** on purpose. Fix the production code and they turn green.

| Test | File / line | Problem |
| ---- | ----------- | ------- |
| `SECSAttrReqCfgTests.ReadConfig_WithQualifierAttributes_ParsesQualifiers` | `SECSAttribute\SECSAttrCfg.cs` – qualifier loop | `if (rc != 0)` after `GetKeyData`, which returns `1` on success. Any section with a `QualAttrN` key fails with `rc=-3` and an empty reason. Should be `rc != 1`. |
| `GemCliConfigTests.RemoteCommandDestination_TrailingSeparator_ShouldBeRemoved` | `GemCliConfig.cs` – `GetRcmdConfig` | `RcmdDestPath.Remove(...)` result is discarded, so a trailing `\` is never stripped. Should be `RcmdDestPath = RcmdDestPath.Remove(...)` (or `TrimEnd`). |
| `MiBTestConfigMsgHandlingTests.GetHandlingFunctionName_ByFrames_MoreFramesThanConfigured_ShouldNotThrowInternally` | `MiBTestConfig.cs` – `GetHandlingFunctionName(string[])` | When more frames are received than `FrameData` entries are configured and all configured ones match, `cfg.FrameData[i]` goes out of range. It returns `-1` (exception) and stops checking the remaining sections. |
| `GemCliConfigTests.ComponentConfig_SpaceAfterComma_SectionNameIsTrimmed` | `GemCliConfig.cs` – `GetGemCliComponentConfig` | The section name after the comma is not trimmed, so `vars.ini, GemVar` fails with -8 "section missing". Should be `items[1].Trim()`. The current real config has no spaces, so it is not hit today. |
| `MiBTestConfigMsgHandlingTests.FrameData_KeysOutOfOrderInFile_AreOrderedByNumber` | `MiBTestConfig.cs` – `ExtractFrameData` | FrameData is ordered by position in the file, not by the N in `FrameDataN`. Writing `FrameData2` before `FrameData1` silently swaps their meaning. |
| `MiBTestConfigGemStateTests.GemStateEnum_TwoNonNumericValues_ReturnsMinus3` | `MiBTestConfig.cs` – `ExtractGemStateEnum` (same pattern in `GetSECSDataTypeEnums`) | `SingleOrDefault` throws when two or more values are non-numeric, so the result is -1 with an exception dump instead of -3. |
| `MiBTestConfigGemStateTests.GetGemStateNumber_UnknownModel_ReturnsMinus2` | `MiBTestConfig.cs` – `GetGemStateNumber` | `.First()` throws when no field matches, so the `== null` → -2 branch is dead code and an unknown model returns -1. `Contains` also matches partial names. |

To run everything except the known bugs:

```
vstest.console.exe ConfigurationManager.Tests.dll /TestCaseFilter:"TestCategory!=KnownBug"
```

Test Explorer: group by **Traits** to see the `KnownBug` category.

## Documented Current Behaviour (not failing)

These tests pin down behaviour that may or may not be intended. They pass today, so a change will be noticed:

* A missing `Reply` key in a `*Cmd` section is **not** an error. `ReplySecName` becomes `""`.
* `EventName` (single) is never populated for ReplyWithEvent sections. Only `EventName1..N` are read, so `GetReplyEventName` returns `null`.
* `LookupCliEventCEID` always returns `-2`, because reading the CLI event to CEID table was disabled in ver 1.0.0.9.
* Calling `GemCliConfig.ReadGemCliConfig()` twice on the same instance fails (`-1`), because the variable dictionary is not cleared.
* `BinaryRecipeFormat`: any non-zero number means binary (`1`). A missing or non-numeric value also defaults to `1`, with a `WARNING` in `GetDLLConfig`'s result. `ReadConfigFileAsync` does not pass that warning on.

* When several `*Cmd` sections share the same `FrameData1`, `GetHandlingFunctionName` returns the **first** one in the file. Section order in MiBTest.ini is therefore part of the protocol (`MiBTestConfigGoldenFileTests`).
* In the real STI CLI configuration, all sub-configurations are sections inside MiBTest.ini itself (`CLIGemVariables`, `CLIGemRemoteCommand`, ...), not separate files.
* The real CLI GEM variable JSON has a trailing comma after the last array item; Newtonsoft.Json accepts it.
* `AttributRequestConfigPath` is misspelt in both the code and the real configuration. Do not fix the spelling in one place only.
* `EventConfig` points to a `CLIGemEvents` section that does not exist in the real file. It is not read since ver 1.0.0.9.
* `RecipeConfig`, `VIDSetMulti` and `MmiWinHandle` exist in the real `[STICLI SECSGEM]` section but are not read by `GemCliConfig`.

## Test Results

Run from the `CSMiBTest-LowRes` folder:

    .\scripts\Run-Tests.ps1 -TestProject "..\ConfigurationManager.Tests\ConfigurationManager.Tests.csproj"
    .\scripts\New-ManagerSummary.ps1 -TestResultsDir "..\ConfigurationManager.Tests\TestResults" -ProjectName "ConfigurationManager.Tests" -FocusClasses "MiBTestConfig,GemCliConfig,JsonVidConfig,SECSAttrReqCfg" -FocusClassDescriptions "<see previous run>"

| File | Audience | What it is |
| ---- | -------- | ---------- |
| `ManagerSummary.html` | Management | One page: test counts, coverage of the main modules, defects |
| `CoverageReport\index.html` | Developers | Line-by-line coverage; red lines were not run by any test |
| `CoverageReport\Summary.txt` | Developers | Coverage percentages as text |
| `*.trx` | Developers, CI | Raw results of every test; opens in Visual Studio |
| `coverage.cobertura.xml` | Tools | Raw coverage data used by the two reports above |
| `defects.json` | Maintained by hand | Defect list shown in `ManagerSummary.html`; set `status` to `Fixed` when a bug is fixed |

Each run overwrites the reports. Commit `TestResults` to keep a history.
