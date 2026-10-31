#ifndef PackageRoot
  #error PackageRoot is required
#endif
#ifndef ReleaseTag
  #error ReleaseTag is required
#endif
[Setup]
#ifdef TestBuild
AppId={{55A11C45-6EAF-4F6A-BDC7-EB299071A26C}
#else
AppId={{D2E2F599-EE03-4A91-8C44-3059A7A8D6CE}
#endif
AppName=dogsc
AppVersion={#ReleaseTag}
AppPublisher=dogsc Windows community
AppPublisherURL=https://github.com/adrian147258/dogsc
DefaultDirName={localappdata}\Programs\dogsc
DefaultGroupName=dogsc
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#PackageRoot}\..\installer-output
OutputBaseFilename=dogsc-{#ReleaseTag}-setup-x64
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
UninstallDisplayIcon={app}\Windows\bin\DogSC.Windows.exe

[Tasks]
Name: desktopicon; Description: Create a desktop shortcut; Flags: unchecked

[Files]
Source: "{#PackageRoot}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\dogsc"; Filename: "{app}\Windows\bin\DogSC.Windows.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\dogsc"; Filename: "{app}\Windows\bin\DogSC.Windows.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
#ifndef TestBuild
Filename: "{app}\Windows\bin\DogSC.Windows.exe"; Description: "Start dogsc"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Windows\bin\DogSC.Windows.exe"; Flags: nowait skipifnotsilent
#endif

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SaveStringToFile(ExpandConstant('{app}\Windows\installed.flag'), 'installed', False);
end;
