[Setup]
AppName=Monitoring HygroVue 5
AppVersion=1.0.0
AppPublisher=PL-MTI EPFL
DefaultDirName={autopf}\MonitoringHygroVue5
DefaultGroupName=Monitoring HygroVue 5
OutputBaseFilename=MonitoringHygroVue5_Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Files]
Source: "C:\tpi-monitoring-app\srs\HydroVue5_monitoring\bin\Release\net8.0-windows\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Monitoring HygroVue 5"; Filename: "{app}\HydroVue5_monitoring.exe"
Name: "{commondesktop}\Monitoring HygroVue 5"; Filename: "{app}\HydroVue5_monitoring.exe"

[Run]
Filename: "{app}\HydroVue5_monitoring.exe"; Description: "Lancer l'application"; Flags: nowait postinstall skipifsilent