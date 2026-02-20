; Script de Instalación Moderno y Dark Mode para MyDesktopOrganizer

#define MyAppName "MyDesktopOrganizer"
#define MyAppVersion "1.0"
#define MyAppPublisher "dvega-uy"
#define MyAppURL "https://github.com/dvega-uy"
#define MyAppExeName "MyDesktopOrganizer.exe"

[Setup]
; --- APARIENCIA MODERNA Y DARK MODE ---
WizardStyle=modern
WizardResizable=no
WizardSizePercent=100,100
; Esto activa el modo oscuro automático si Windows está en modo oscuro
AllowNoIcons=yes
Compression=lzma2/ultra64
SolidCompression=yes

; --- DETALLES DEL PROYECTO ---
AppId={{A1B2C3D4-E5F6-7890-1234-567890ABCDEF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
LicenseFile=license.txt
DefaultDirName={userpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Instalar en AppData (No pide Admin, más moderno y seguro)
PrivilegesRequired=lowest
OutputBaseFilename=Instalador_MyDesktopOrganizer
SetupIconFile=MyDesktopOrganizer.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; --- IMÁGENES (Opcional: Si no tienes, Inno usa las default modernas) ---
; WizardImageFile=sidebar.bmp
; WizardSmallImageFile=small.bmp

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; AQUÍ BUSCA TU EXE COMPILADO. Asegúrate que la ruta coincida con lo que generó 'dotnet publish'
Source: "bin\Release\net8.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "MyDesktopOrganizer.ico"; DestDir: "{app}"; Flags: ignoreversion
; Nota: No necesitamos copiar DLLs extra porque usamos 'PublishSingleFile'

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\MyDesktopOrganizer.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\MyDesktopOrganizer.ico"

[Run]
; Ejecutar al finalizar la instalación
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
