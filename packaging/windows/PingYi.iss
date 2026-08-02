#define MyAppName "屏译"
#ifndef MyAppVersion
#define MyAppVersion "0.1.0"
#endif
#ifndef SourceDir
#define SourceDir "..\..\artifacts\publish\win-x64-0.1.0"
#endif
#define MyAppPublisher "PingYi contributors"
#define MyAppExeName "PingYi.App.exe"

[Setup]
AppId={{CB72A4A2-277B-4763-9AC2-DF0B17107579}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PingYi
DefaultGroupName={#MyAppName}
OutputBaseFilename=PingYi-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
