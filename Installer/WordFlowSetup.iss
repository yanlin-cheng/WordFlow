; WordFlow 安装程序脚本 - Inno Setup
; 编译后生成 Setup.exe 安装包
; 自包含版本 - 无需额外安装 .NET Runtime

#define MyAppName "WordFlow"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "WordFlow Team"
#define MyAppURL "https://github.com/wanddream/WordFlowV2"
#define MyAppExeName "WordFlow.exe"

[Setup]
; 基本信息
; 注意：请生成一个唯一的 GUID，可以使用在线工具生成：https://www.guidgen.com/
AppId={{B2C3D4E5-F6A7-8901-BCDE-F12345678901}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\WordFlow
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
OutputDir=..\Output\Installer
OutputBaseFilename=WordFlow_Setup
SetupIconFile=..\Resources\icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

; 要求管理员权限（用于安装到 Program Files）
PrivilegesRequired=admin

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; 主程序（自包含发布 - 包含所有 .NET 运行时）
Source: "..\publish_installer\WordFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish_installer\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish_installer\*.pdb"; DestDir: "{app}"; Flags: ignoreversion

; PythonASR 文件夹（包含嵌入 Python 环境）
Source: "..\publish_installer\PythonASR\*"; DestDir: "{app}\PythonASR"; Flags: ignoreversion recursesubdirs createallsubdirs

; 数据文件夹（配置文件）
Source: "..\publish_installer\Data\*"; DestDir: "{app}\Data"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 安装完成后询问是否运行
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]

var
  DownloadingModel: Boolean;

// 安装步骤处理
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // 安装完成后显示提示
    MsgBox('WordFlow 安装完成！'#13#10#13#10 +
           '• 已包含 .NET 8 运行时，无需额外安装'#13#10 +
           '• 已包含 Python 语音识别环境'#13#10#13#10 +
           '首次运行时，请点击「设置」→「模型管理」下载语音模型。', mbInformation, MB_OK);
  end;
end;

// 卸载时清理用户数据（可选）
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // 询问是否删除用户数据
    if MsgBox('是否同时删除用户数据（个人词典、历史记录等）？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      // 删除用户数据目录
      // 注意：用户数据在 Documents\WordFlow，不在安装目录
    end;
  end;
end;
