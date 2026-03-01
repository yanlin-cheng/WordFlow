; WordFlow 安装程序脚本 - Inno Setup
; 编译后生成 Setup.exe 安装包
; 支持在安装过程中直接下载模型和 .NET Runtime

#define MyAppName "WordFlow"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WordFlow Team"
#define MyAppURL "https://gitee.com/cheng-yanlin/WordFlow-Release"
#define MyAppExeName "WordFlow.exe"

[Setup]
; 基本信息
; 注意：请生成一个唯一的 GUID，可以使用在线工具生成：https://www.guidgen.com/
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
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
; 主程序（从 Output 目录复制，已精简）
Source: "..\Output\Installer\WordFlow\WordFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Output\Installer\WordFlow\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Output\Installer\WordFlow\*.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Output\Installer\WordFlow\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Output\Installer\WordFlow\win-x64\*"; DestDir: "{app}\win-x64"; Flags: ignoreversion recursesubdirs createallsubdirs

; PythonASR 文件夹（从 Output 目录复制，已精简 python 环境）
Source: "..\Output\Installer\WordFlow\PythonASR\*"; DestDir: "{app}\PythonASR"; Flags: ignoreversion recursesubdirs createallsubdirs

; 数据文件夹（配置文件）
Source: "..\Output\Installer\WordFlow\Data\*"; DestDir: "{app}\Data"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 安装完成后询问是否运行
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]

var
  ModelPage: TInputOptionWizardPage;
  ModelDownloaded: Boolean;
  DownloadingModel: Boolean;
  DotNetDownloading: Boolean;
  ProgressBar: TNewProgressBar;
  StatusLabel: TNewStaticText;

// 检测是否已安装 .NET 8 Runtime
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
  ExecResult: Boolean;
  RegKey: String;
begin
  // 方法 1：检查注册表
  RegKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost';
  if RegKeyExists(HKLM, RegKey) then
  begin
    Result := True;
    Exit;
  end;
  
  // 方法 2：尝试运行 dotnet 命令
  ExecResult := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := ExecResult and (ResultCode = 0);
end;

// 静默安装 .NET Runtime（带进度显示）
function InstallDotNetRuntime(DownloadUrl: String): Boolean;
var
  InstallerPath: String;
  PowerShellCmd: String;
  ResultCode: Integer;
begin
  Result := False;
  InstallerPath := ExpandConstant('{tmp}\dotnet8-runtime.exe');
  
  // 使用 PowerShell 下载（后台，无进度条）
  PowerShellCmd := Format('powershell -Command "Invoke-WebRequest -Uri ''%s'' -OutFile ''%s'' -UseBasicParsing"', [DownloadUrl, InstallerPath]);
  
  Log('正在下载 .NET Runtime...');
  if Exec('cmd.exe', '/c ' + PowerShellCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if FileExists(InstallerPath) then
    begin
      Log('正在安装 .NET Runtime...');
      // 静默安装
      if Exec(InstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        DeleteFile(InstallerPath);
        if ResultCode = 0 then
        begin
          Result := True;
          Log('.NET Runtime 安装成功');
        end;
      end;
    end;
  end;
end;

// 下载并安装模型
function DownloadAndInstallModel(ModelId: String; InstallDir: String): Boolean;
var
  PythonPath: String;
  ScriptPath: String;
  Params: String;
  Output: String;
  ResultCode: Integer;
begin
  Result := False;
  DownloadingModel := True;
  
  try
    // 构建路径
    PythonPath := InstallDir + '\PythonASR\python\python.exe';
    ScriptPath := InstallDir + '\PythonASR\download_model.py';
    Params := '"' + ModelId + '" --models-dir "' + InstallDir + '\PythonASR\models"';
    
    // 检查文件是否存在
    if not FileExists(PythonPath) then
    begin
      MsgBox('错误：未找到嵌入式 Python 解释器', mbError, MB_OK);
      Exit;
    end;
    
    if not FileExists(ScriptPath) then
    begin
      MsgBox('错误：未找到模型下载脚本', mbError, MB_OK);
      Exit;
    end;
    
    // 显示下载提示
    MsgBox('即将开始下载模型（约 200MB），请确保网络连接稳定。'#13#10#13#10 +
           '下载过程可能需要几分钟时间，请耐心等待。'#13#10#13#10 +
           '点击"确定"开始下载...', mbInformation, MB_OK);
    
    // 执行下载命令
    if Exec(PythonPath, Params, InstallDir, SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode = 0 then
      begin
        Result := True;
        MsgBox('模型下载安装成功！', mbInformation, MB_OK);
      end
      else
      begin
        MsgBox('模型下载失败，错误代码: ' + IntToStr(ResultCode), mbError, MB_OK);
      end;
    end
    else
    begin
      MsgBox('无法启动模型下载进程', mbError, MB_OK);
    end;
    
  except
    MsgBox('下载过程中发生异常', mbError, MB_OK);
  end;
  
  DownloadingModel := False;
end;

// 初始化安装向导
procedure InitializeWizard;
begin
  // 创建进度条（用于显示 .NET 和模型下载进度）
  ProgressBar := TNewProgressBar.Create(WizardForm);
  ProgressBar.Parent := WizardForm.InstallingPage;
  ProgressBar.Left := Round(WizardForm.InstallingPage.Width * 0.1);
  ProgressBar.Top := Round(WizardForm.InstallingPage.Height * 0.3);
  ProgressBar.Width := Round(WizardForm.InstallingPage.Width * 0.8);
  ProgressBar.Height := 20;
  ProgressBar.Min := 0;
  ProgressBar.Max := 100;
  ProgressBar.Visible := False;
  
  // 创建状态标签
  StatusLabel := TNewStaticText.Create(WizardForm);
  StatusLabel.Parent := WizardForm.InstallingPage;
  StatusLabel.Left := Round(WizardForm.InstallingPage.Width * 0.1);
  StatusLabel.Top := Round(WizardForm.InstallingPage.Height * 0.25);
  StatusLabel.Width := Round(WizardForm.InstallingPage.Width * 0.8);
  StatusLabel.Height := 20;
  StatusLabel.Caption := '';
  StatusLabel.Visible := False;
  
  // 创建模型选择页面
  ModelPage := CreateInputOptionPage(wpSelectDir,
    '选择语音识别模型', '请选择要下载的语音识别模型',
    'WordFlow 需要语音识别模型才能正常工作。选择一个模型在安装过程中下载。',
    False, False);
  
  // 添加模型选项
  ModelPage.Add('Paraformer-zh (中文语音识别模型，约 200MB) [推荐]');
  ModelPage.Values[0] := True;  // 默认选中
  
  ModelDownloaded := False;
  DotNetDownloading := False;
end;

// 带进度的 .NET 安装
function InstallDotNetWithProgress: Boolean;
var
  DownloadUrl: String;
  InstallerPath: String;
  PowerShellCmd: String;
  ResultCode: Integer;
  i: Integer;
begin
  Result := False;
  DownloadUrl := 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.24/windowsdesktop-runtime-8.0.24-win-x64.exe';
  InstallerPath := ExpandConstant('{tmp}\dotnet8-runtime.exe');
  
  // 显示进度条和状态
  ProgressBar.Visible := True;
  StatusLabel.Visible := True;
  WizardForm.NextButton.Enabled := False;
  
  // 下载阶段（0-50%）
  StatusLabel.Caption := '正在检测 .NET 8 运行库...';
  WizardForm.StatusLabel.Caption := '正在下载 .NET 8 运行库...';
  ProgressBar.Position := 0;
  
  PowerShellCmd := Format('powershell -Command "$ProgressPreference = ''SilentlyContinue''; Invoke-WebRequest -Uri ''%s'' -OutFile ''%s'' -UseBasicParsing"', [DownloadUrl, InstallerPath]);
  
  if Exec('cmd.exe', '/c ' + PowerShellCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if FileExists(InstallerPath) then
    begin
      // 模拟下载进度
      for i := 0 to 50 do
      begin
        ProgressBar.Position := i;
        Sleep(100);
      end;
      
      // 安装阶段（50-100%）
      StatusLabel.Caption := '正在安装 .NET 8 运行库...';
      WizardForm.StatusLabel.Caption := '正在安装 .NET 8 运行库...';
      
      if Exec(InstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        DeleteFile(InstallerPath);
        for i := 50 to 100 do
        begin
          ProgressBar.Position := i;
          Sleep(50);
        end;
        
        if ResultCode = 0 then
        begin
          Result := True;
          StatusLabel.Caption := '.NET 8 运行库安装完成';
          Log('.NET Runtime 安装成功');
        end
        else
        begin
          StatusLabel.Caption := '.NET 8 运行库安装失败（错误代码：' + IntToStr(ResultCode) + '）';
          Log('.NET Runtime 安装可能失败，错误代码：' + IntToStr(ResultCode));
        end;
      end;
    end
    else
    begin
      StatusLabel.Caption := '.NET 8 运行库下载失败';
      Log('.NET Runtime 下载失败');
    end;
  end
  else
  begin
    StatusLabel.Caption := '.NET 8 运行库下载失败';
    Log('.NET Runtime 下载失败');
  end;
  
  // 保持进度条显示几秒钟
  Sleep(1000);
  ProgressBar.Visible := False;
  StatusLabel.Visible := False;
  WizardForm.NextButton.Enabled := True;
end;

// 下一步按钮点击时检查 .NET 和模型下载
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  if CurPageID = wpReady then
  begin
    // 检查 .NET 8 是否已安装
    if not IsDotNet8Installed() then
    begin
      // 显示提示
      if MsgBox('检测到您的系统未安装 .NET 8 运行库。'#13#10#13#10 +
                'WordFlow 需要 .NET 8 才能运行。'#13#10#13#10 +
                '是否现在自动下载并安装？（约 60MB）'#13#10#13#10 +
                '如果选择"否"，安装后需要手动安装 .NET 8。',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        // 带进度条安装 .NET
        InstallDotNetWithProgress;
      end
      else
      begin
        Log('用户选择不安装 .NET Runtime');
        MsgBox('请注意：安装完成后需要手动安装 .NET 8 运行库才能启动程序。'#13#10 +
               '下载地址：https://dotnet.microsoft.com/download/dotnet/8.0',
               mbConfirmation, MB_OK);
      end;
    end;
  end;
end;

// 安装步骤处理
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // 安装文件
  end
  else if CurStep = ssPostInstall then
  begin
    // 安装完成后显示提示
    MsgBox('WordFlow 安装完成！'#13#10#13#10 +
           '首次运行时会自动启动 Python 语音识别服务。'#13#10#13#10 +
           '请在程序设置中下载语音识别模型。', mbInformation, MB_OK);
  end;
end;

// 窗口关闭处理
procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if DownloadingModel then
  begin
    Confirm := False;
    MsgBox('正在下载模型，请等待下载完成或在任务管理器中结束进程。', mbInformation, MB_OK);
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
