; WordFlow 安装程序脚本 - Inno Setup
; 编译后生成 Setup.exe 安装包
; 自包含版本 - 无需额外安装 .NET Runtime

#define MyAppName "WordFlow"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "WordFlow Team"
#define MyAppURL "https://github.com/yanlin-cheng/WordFlow"
#define MyAppExeName "WordFlow.exe"

[Setup]
; 基本信息
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

; 语言设置：不显示语言选择对话框，使用自定义语言选择页面
UsePreviousLanguage=yes

[Languages]
; 支持多语言，Inno Setup 会根据系统语言自动选择
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
; 自定义多语言消息
chinesesimplified.InstallCompleteTitle=安装完成！
chinesesimplified.InstallCompleteDesc=WordFlow 已成功安装到您的计算机
chinesesimplified.LaunchOnFinish=安装完成后启动 WordFlow

en.InstallCompleteTitle=Installation Complete!
en.InstallCompleteDesc=WordFlow has been successfully installed on your computer
en.LaunchOnFinish=Launch WordFlow after installation

[TASKS]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "launchonfinish"; Description: "{cm:LaunchOnFinish}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone

[Files]
; 主程序（自包含发布 - 包含所有 .NET 运行时）
Source: "..\publish_installer\WordFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish_installer\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish_installer\*.pdb"; DestDir: "{app}"; Flags: ignoreversion

; 多语言资源文件（卫星程序集）- 只保留英文，中文是默认语言已包含在主程序中
; 已排除其他语言包（de, es, fr, it, ja, ko, pl, pt-BR, ru, cs, tr, zh-Hant）以节省约 15MB 空间
Source: "..\publish_installer\en\*"; DestDir: "{app}\en"; Flags: ignoreversion recursesubdirs createallsubdirs

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
  InstalledLanguageCode: String;
  LanguagePage: TWizardPage;
  LanguageOption1: TRadioButton;
  LanguageOption2: TRadioButton;
  LanguageLabel: TLabel;

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
  
  EnsureDirectoryExists(SettingsDir);
  
  SettingsContent := '{' + #13#10 +
    '  "HotkeyCode": 192,' + #13#10 +
    '  "AutoStart": false,' + #13#10 +
    '  "MinimizeToTray": true,' + #13#10 +
    '  "StartMinimized": false,' + #13#10 +
    '  "CloseAction": 2,' + #13#10 +
    '  "LanguageCode": "' + LanguageCode + '",' + #13#10 +
    '  "InstallerLanguageCode": "' + LanguageCode + '",' + #13#10 +
    '  "HasCompletedFirstRun": false' + #13#10 +
  '}';
  
  SaveStringToFile(SettingsPath, SettingsContent, False);
  Log('语言设置已写入：LanguageCode=' + LanguageCode + ', InstallerLanguageCode=' + LanguageCode);
end;

// 获取用户选择的语言代码
function GetSelectedLanguageCode: String;
begin
  if LanguageOption2.Checked then
    Result := 'en-US'
  else
    Result := 'zh-CN';
end;

// 当页面变化时（用户切换到语言选择页面）
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = LanguagePage.ID then
  begin
    InstalledLanguageCode := GetSelectedLanguageCode;
    Log('用户当前选择的语言：' + InstalledLanguageCode);
  end;
end;

// 在点击"下一步"之前验证
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  if CurPageID = LanguagePage.ID then
  begin
    InstalledLanguageCode := GetSelectedLanguageCode;
    Log('用户确认选择的语言：' + InstalledLanguageCode);
  end;
end;

// 检测系统语言并返回默认选择
function GetSystemLanguageCode: String;
begin
  if ExpandConstant('{language}') = 'en' then
    Result := 'en-US'
  else
    Result := 'zh-CN';
end;

// 中文选项被选中（必须先定义，才能在 CreateLanguagePage 中引用）
procedure LanguageOption1Click(Sender: TObject);
begin
  if LanguageOption1.Checked then
    LanguageOption2.Checked := False;
  InstalledLanguageCode := 'zh-CN';
  Log('用户选择：简体中文');
end;

// 英文选项被选中（必须先定义，才能在 CreateLanguagePage 中引用）
procedure LanguageOption2Click(Sender: TObject);
begin
  if LanguageOption2.Checked then
    LanguageOption1.Checked := False;
  InstalledLanguageCode := 'en-US';
  Log('User selected: English');
end;

// 创建自定义语言选择页面
procedure CreateLanguagePage;
var
  IsEnglishSystem: Boolean;
begin
  LanguagePage := CreateCustomPage(wpWelcome, 'Select Language / 选择语言', 'Please select the interface language for WordFlow.' + #13#10 + '请选择 WordFlow 的界面语言。');
  
  LanguageLabel := TLabel.Create(LanguagePage);
  LanguageLabel.Caption := 'This language can be changed later in the settings.' + #13#10 + '此语言可以在安装后的设置中更改。';
  LanguageLabel.Font.Style := [fsItalic];
  LanguageLabel.Font.Color := clGray;
  LanguageLabel.Left := 10;
  LanguageLabel.Top := 10;
  LanguageLabel.Width := LanguagePage.SurfaceWidth - 20;
  LanguageLabel.Height := 30;
  LanguageLabel.WordWrap := True;
  LanguageLabel.Parent := LanguagePage.Surface;
  
  IsEnglishSystem := (ExpandConstant('{language}') = 'en');
  
  LanguageOption1 := TRadioButton.Create(LanguagePage);
  LanguageOption1.Caption := 'Chinese (简体中文)';
  LanguageOption1.Left := 10;
  LanguageOption1.Top := 50;
  LanguageOption1.Width := LanguagePage.SurfaceWidth - 20;
  LanguageOption1.Height := 30;
  LanguageOption1.Checked := not IsEnglishSystem;
  LanguageOption1.Parent := LanguagePage.Surface;
  
  LanguageOption2 := TRadioButton.Create(LanguagePage);
  LanguageOption2.Caption := 'English';
  LanguageOption2.Left := 10;
  LanguageOption2.Top := 90;
  LanguageOption2.Width := LanguagePage.SurfaceWidth - 20;
  LanguageOption2.Height := 30;
  LanguageOption2.Checked := IsEnglishSystem;
  LanguageOption2.Parent := LanguagePage.Surface;
  
  LanguageOption1.OnClick := @LanguageOption1Click;
  LanguageOption2.OnClick := @LanguageOption2Click;
end;

procedure InitializeWizard;
begin
  CreateLanguagePage;
  InstalledLanguageCode := GetSystemLanguageCode;
  Log('系统语言代码：' + InstalledLanguageCode);
end;

// 安装步骤处理
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    WriteLanguageSetting(InstalledLanguageCode);
    Log('已写入语言设置：' + InstalledLanguageCode);
  end;
  
  if CurStep = ssPostInstall then
  begin
    MsgBox(ExpandConstant('{cm:InstallCompleteTitle}') + #13#10#13#10 +
           '* {cm:InstallCompleteDesc}'#13#10#13#10 +
           '* {#MyAppName} v{#MyAppVersion}', mbInformation, MB_OK);
  end;
end;

// 卸载时清理用户数据（可选）
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('是否同时删除用户数据（个人词典、历史记录等）？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      // 删除用户数据目录
    end;
  end;
end;
