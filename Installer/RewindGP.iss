; Rewind GP setup: a per-user install (no admin) into %LocalAppData%\Programs\RewindGP.
;
; The app keeps its saves and season packs in its own folder and the in-app updater extracts
; each release's zip over that folder, so the install has to stay writable by the user - hence
; per-user. The zip stays the update channel; this setup only does the first install, the
; shortcuts, the .rwgp association and the uninstall.
;
; Build with Installer\Build-Installer.ps1, or from the release workflow:
;   ISCC.exe /DSourceDir=<app build output> /DAppVersion=2.4.0 /DTag=v2.4 RewindGP.iss

#ifndef SourceDir
  #define SourceDir AddBackslash(SourcePath) + "..\AMS2ChEd\bin\Release\net8.0-windows"
#endif
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString(AddBackslash(SourceDir) + "AMS2ChEd.exe")
#endif
#ifndef Tag
  #define Tag "v" + AppVersion
#endif

#define AppExe "AMS2ChEd.exe"
#define DotNetDownloadUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
#define DotNetPageUrl "https://dotnet.microsoft.com/download/dotnet/8.0"

[Setup]
AppId={{6139E9D5-C2B7-4A99-ACE1-B10102D36B1B}
AppName=Rewind GP
AppVersion={#AppVersion}
AppVerName=Rewind GP {#Tag}
AppPublisher=Massimo Franciosa
DefaultDirName={localappdata}\Programs\RewindGP
DisableProgramGroupPage=yes
DefaultGroupName=Rewind GP
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#SourcePath}..\AMS2ChEd\StaticFiles\rewind_gp_logo_128.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName=Rewind GP
OutputBaseFilename=RewindGP-Setup-{#Tag}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Everything the release zip holds. The player's own data (saves, installed season packs) and
; leftovers of local test runs are never shipped, so a reinstall can't overwrite them.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; \
  Excludes: "\Saves,\Seasons,\SavesBakedOnly,\SeasonsBakedOnly,*.lnk"

[Icons]
Name: "{autoprograms}\Rewind GP"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\Rewind GP"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; The same .rwgp association FileAssociationHelper.Register writes on every launch, here so it
; works before the first launch and is removed by the uninstall.
Root: HKCU; Subkey: "Software\Classes\.rwgp"; ValueType: string; ValueName: ""; ValueData: "RewindGP.SeasonPack"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.rwgp"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-rewindgp-season"
Root: HKCU; Subkey: "Software\Classes\RewindGP.SeasonPack"; ValueType: string; ValueName: ""; ValueData: "Rewind GP Season Pack"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\RewindGP.SeasonPack\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"",0"
Root: HKCU; Subkey: "Software\Classes\RewindGP.SeasonPack\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Rewind GP"
Root: HKCU; Subkey: "Software\Classes\RewindGP.SeasonPack\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,Rewind GP}"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; The in-app updater adds files the uninstall log doesn't know about, so each top-level item the
; app ships is removed whole. Only names of the build output are listed, so nothing else in a
; folder the player picked is touched, and Saves/Seasons stay unless the player asks otherwise.
#define FindHandle
#define FindResult
#sub AddShippedItem
  #define Name FindGetFileName(FindHandle)
  #define Lower LowerCase(Name)
  #if Name != "." && Name != ".." && Lower != "saves" && Lower != "seasons" && Lower != "savesbakedonly" && Lower != "seasonsbakedonly" && Pos(".lnk", Lower) == 0
Type: filesandordirs; Name: "{app}\{#Name}"
  #endif
#endsub
#for {FindHandle = FindResult = FindFirst(AddBackslash(SourceDir) + "*", faAnyFile); FindResult; FindResult = FindNext(FindHandle)} AddShippedItem
#if FindHandle
  #expr FindClose(FindHandle)
#endif

[Code]
var
  DownloadPage: TDownloadWizardPage;
  DeleteUserData: Boolean;

{ --- .NET 8 Desktop Runtime ------------------------------------------------------------ }

function IsDotNetDesktop8Installed: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Rec: TFindRec;
begin
  Result := False;
  if RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '8.' then
      begin
        Result := True;
        Exit;
      end;

  { the registry entries are missing on some installs: look at the runtime folders too }
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), Rec) then
  begin
    Result := True;
    FindClose(Rec);
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), 'Downloading the .NET 8 Desktop Runtime...', @OnDownloadProgress);
end;

{ Downloads Microsoft's runtime installer and runs it; it asks for admin rights itself. }
function InstallDotNet: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  DownloadPage.Clear;
  DownloadPage.Add('{#DotNetDownloadUrl}', 'windowsdesktop-runtime-8-win-x64.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      if not DownloadPage.AbortedByUser then
        MsgBox('The .NET 8 Desktop Runtime could not be downloaded:'#13#10 + GetExceptionMessage, mbError, MB_OK);
      Exit;
    end;
  finally
    DownloadPage.Hide;
  end;

  if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime-8-win-x64.exe'), '/install /passive /norestart', '',
                   SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    Exit;

  Result := IsDotNetDesktop8Installed;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if (CurPageID <> wpReady) or IsDotNetDesktop8Installed then
    Exit;

  if MsgBox('Rewind GP needs the Microsoft .NET 8 Desktop Runtime, which is not installed.'#13#10#13#10 +
            'Download and install it now? (Windows will ask for permission.)', mbConfirmation, MB_YESNO) = IDYES then
    if InstallDotNet then
      Exit;

  if MsgBox('Rewind GP will not start until the .NET 8 Desktop Runtime is installed.'#13#10#13#10 +
            'Continue anyway? You can get the runtime from ' + '{#DotNetPageUrl}', mbConfirmation, MB_YESNO) = IDNO then
  begin
    ShellExec('open', '{#DotNetPageUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;

{ --- Uninstall ----------------------------------------------------------------------- }

function IsAppRunning: Boolean;
var
  Locator, Service, Processes: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = ''{#AppExe}''');
    Result := Processes.Count > 0;
  except
    { can't tell: let the uninstall go on }
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  while IsAppRunning do
    if MsgBox('Rewind GP is running. Close it, then click Retry.', mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
end;

{ Everything in the folder but the item named Keep; the folder itself goes once empty. }
procedure DelTreeExcept(const Folder, Keep: String);
var
  Rec: TFindRec;
begin
  if FindFirst(AddBackslash(Folder) + '*', Rec) then
  try
    repeat
      if (Rec.Name <> '.') and (Rec.Name <> '..') and (CompareText(Rec.Name, Keep) <> 0) then
        DelTree(AddBackslash(Folder) + Rec.Name, (Rec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0, True, True);
    until not FindNext(Rec);
  finally
    FindClose(Rec);
  end;
  RemoveDir(Folder);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteUserData := (not UninstallSilent) and
      (MsgBox('Also delete your saved games, installed season packs and Rewind GP settings?'#13#10#13#10 +
              'Choose No to keep them for a later reinstall.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    if DeleteUserData then
    begin
      DelTree(ExpandConstant('{app}\Saves'), True, True, True);
      DelTree(ExpandConstant('{app}\Seasons'), True, True, True);
      DelTree(ExpandConstant('{app}\SavesBakedOnly'), True, True, True);
      DelTree(ExpandConstant('{app}\SeasonsBakedOnly'), True, True, True);
      { preferences, image cache, drawn artwork, logs - but not the backups of the AMS2 vehicle
        paks the app patched: without them those paks could never be restored }
      DelTreeExcept(ExpandConstant('{localappdata}\RewindGP'), 'vehicle_pak_backups');
      DelTree(ExpandConstant('{localappdata}\Ams2Interop'), True, True, True);
    end;
    { gone only when nothing the player keeps is left in it }
    RemoveDir(ExpandConstant('{app}'));
  end;
end;
