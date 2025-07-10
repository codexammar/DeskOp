; DeskOpInstaller.iss - Creates installer with uninstaller for DeskOp

[Setup]
AppName=DeskOp
AppVersion=1.0.0
DefaultDirName={pf}\DeskOp
DefaultGroupName=DeskOp
OutputDir=InstallerBuild
OutputBaseFilename=DeskOpSetup
SetupIconFile=InstallerBuild\Assets\deskop.ico
Compression=lzma
SolidCompression=yes

[Files]
Source: "InstallerBuild\DeskOp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "InstallerBuild\Assets\deskop.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "InstallerBuild\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DeskOp"; Filename: "{app}\DeskOp.exe"; IconFilename: "{app}\Assets\deskop.ico"
Name: "{group}\Uninstall DeskOp"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\DeskOp.exe"; Description: "Launch DeskOp"; Flags: nowait postinstall skipifsilent
