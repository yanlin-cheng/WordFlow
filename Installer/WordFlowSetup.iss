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
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"

[CustomMessages]
; 自定义多语言消息
chinesesimplified.SelectLanguageTitle=选择界面语言
chinesesimplified.SelectLanguageDesc=请选择 WordFlow 的界面语言：
chinesesimplified.LanguageLabel=语言：
chinesesimplified.LanguageHint=您可以在安装后在设置中随时更改此选项
chinesesimplified.InstallCompleteTitle=安装完成！
chinesesimplified.InstallCompleteDesc=WordFlow 已成功安装到您的计算机
chinesesimplified.LaunchOnFinish=安装完成后启动 WordFlow

ja.SelectLanguageTitle=インターフェース言語を選択
ja.SelectLanguageDesc=WordFlow のインターフェース言語を選択してください：
ja.LanguageLabel=言語：
ja.LanguageHint=このオプションは設定でいつでも変更できます
ja.InstallCompleteTitle=インストール完了！
ja.InstallCompleteDesc=WordFlow は正常にインストールされました
ja.LaunchOnFinish=インストール後に WordFlow を起動

ko.SelectLanguageTitle=인터페이스 언어 선택
ko.SelectLanguageDesc=WordFlow 의 인터페이스 언어를 선택하세요:
ko.LanguageLabel=언어:
ko.LanguageHint=이 옵션은 설정에서 언제든지 변경할 수 있습니다
ko.InstallCompleteTitle=설치 완료!
ko.InstallCompleteDesc=WordFlow 이 (가) 성공적으로 설치되었습니다
ko.LaunchOnFinish=설치 후 WordFlow 실행


[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "launchonfinish"; Description: "{cm:LaunchOnFinish}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone

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
; 安装完成后启动（由用户勾选决定是否启动）
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent; Check: WizardIsTaskSelected('launchonfinish')

[Code]
var
  DownloadingModel: Boolean;
  SelectedLanguageCode: String;

// 获取配置文件路径
function GetSettingsPath: String;
begin
  Result := ExpandConstant('{userappdata}\WordFlow\settings.json');
end;

// 确保目录存在
procedure EnsureDirectoryExists(const DirPath: String);
begin
  if not DirExists(DirPath) then
    CreateDir(DirPath);
end;

// 写入语言设置到配置文件
procedure WriteLanguageSetting(const LanguageCode: String);
var
  SettingsDir: String;
  SettingsPath: String;
  SettingsContent: String;
begin
  SettingsDir := ExpandConstant('{userappdata}\WordFlow');
  SettingsPath := SettingsDir + '\settings.json';
  
  // 确保目录存在
  EnsureDirectoryExists(SettingsDir);
  
  // 创建配置文件内容（如果文件已存在，读取并修改 LanguageCode）
  if FileExists(SettingsPath) then
  begin
    // 读取现有内容并修改 LanguageCode
    SettingsContent := '{' + #13#10 +
      '  "HotkeyCode": 192,' + #13#10 +
      '  "AutoStart": false,' + #13#10 +
      '  "MinimizeToTray": true,' + #13#10 +
      '  "StartMinimized": false,' + #13#10 +
      '  "CloseAction": 2,' + #13#10 +
      '  "LanguageCode": "' + LanguageCode + '"' + #13#10 +
    '}';
  end
  else
  begin
    // 创建新文件
    SettingsContent := '{' + #13#10 +
      '  "HotkeyCode": 192,' + #13#10 +
      '  "AutoStart": false,' + #13#10 +
      '  "MinimizeToTray": true,' + #13#10 +
      '  "StartMinimized": false,' + #13#10 +
      '  "CloseAction": 2,' + #13#10 +
      '  "LanguageCode": "' + LanguageCode + '"' + #13#10 +
    '}';
  end;
  
  // 写入文件
  SaveStringToFile(SettingsPath, SettingsContent, False);
  Log('语言设置已写入：' + LanguageCode);
end;

// 根据 Inno Setup 的语言代码获取应用语言代码
function GetAppLanguageCode(const SetupLanguage: String): String;
begin
  if SetupLanguage = 'chinesesimplified' then
    Result := 'zh-CN'
  else if SetupLanguage = 'en' then
    Result := 'en-US'
  else if SetupLanguage = 'ja' then
    Result := 'ja-JP'
  else if SetupLanguage = 'ko' then
    Result := 'ko-KR'
  else
    Result := 'zh-CN';  // 默认简体中文
end;

procedure InitializeWizard;
begin
  // 获取当前 Inno Setup 使用的语言代码
  SelectedLanguageCode := GetAppLanguageCode(ExpandConstant('{language}'));
  Log('Inno Setup 语言：' + ExpandConstant('{language}'));
  Log('应用语言代码：' + SelectedLanguageCode);
end;

// 安装步骤处理
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // 在安装前写入语言设置（使用 Inno Setup 选择的语言）
    WriteLanguageSetting(SelectedLanguageCode);
    Log('已写入语言设置：' + SelectedLanguageCode);
  end;
  
  if CurStep = ssPostInstall then
  begin
    // 安装完成后显示提示
    MsgBox(ExpandConstant('{cm:InstallCompleteTitle}') + #13#10#13#10 +
           '• {cm:InstallCompleteDesc}'#13#10#13#10 +
           '• {#MyAppName} v{#MyAppVersion}', mbInformation, MB_OK);
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
