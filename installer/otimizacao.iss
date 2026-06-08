; Instalador do Agente de Otimização e Confiabilidade de Hardware (Inno Setup 6).
;
; Empacota os binários self-contained (CLI + UI) — não exigem .NET na máquina
; alvo — com atalhos, opção de PATH e desinstalador.
;
; Gere com:  scripts\gerar-instalador.ps1   (publica os binários e compila este script)
; ou direto: ISCC.exe installer\otimizacao.iss
;
; Observação: este arquivo é salvo com BOM UTF-8 para que o Inno leia os acentos.

#define AppNome "Agente de Otimização de Hardware"
#define AppVersao "0.1.0"
#define Publicador "mixelcosta"
#define RepoUrl "https://github.com/mixelcosta/otimizacao"
#define CliExe "HardwareOptimizer.Cli.exe"
#define UiExe "HardwareOptimizer.App.exe"

[Setup]
AppId={{8E9B2C4A-1D3F-4A6B-9C2E-7F5A1B3D6E80}
AppName={#AppNome}
AppVersion={#AppVersao}
AppPublisher={#Publicador}
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}
DefaultDirName={autopf}\OtimizacaoHardware
DefaultGroupName={#AppNome}
AllowNoIcons=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=OtimizacaoHardware-Setup-{#AppVersao}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
ChangesEnvironment=yes
WizardStyle=modern
UninstallDisplayName={#AppNome}
VersionInfoVersion={#AppVersao}
VersionInfoCompany={#Publicador}

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "addtopath"; Description: "Adicionar a CLI ({#CliExe}) ao PATH do sistema"; GroupDescription: "Integração:"; Flags: unchecked

[Files]
; Binários self-contained (publicados por scripts\publish.ps1 -ComUI)
Source: "..\artifacts\hwopt-ui-win-x64\{#UiExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\hwopt-cli-win-x64\{#CliExe}"; DestDir: "{app}"; Flags: ignoreversion
; Documentação de apoio
Source: "..\README.md"; DestDir: "{app}"; DestName: "LEIA-ME.md"; Flags: ignoreversion isreadme
Source: "..\docs\MANUAL.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\INSTALACAO.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppNome}"; Filename: "{app}\{#UiExe}"; Comment: "Abrir a interface do otimizador"
Name: "{group}\Prompt do Otimizador (CLI)"; Filename: "{cmd}"; Parameters: "/k cd /d ""{app}"" && {#CliExe} ajuda"; Comment: "Linha de comando do otimizador"
Name: "{group}\Manual de uso"; Filename: "{app}\docs\MANUAL.md"
Name: "{group}\Desinstalar {#AppNome}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppNome}"; Filename: "{app}\{#UiExe}"; Tasks: desktopicon

[Registry]
; Acrescenta o diretório de instalação ao PATH do sistema (opcional, sem duplicar).
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
  ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; \
  Tasks: addtopath; Check: NeedsAddPath(ExpandConstant('{app}'))

[Run]
Filename: "{app}\{#UiExe}"; Description: "Abrir o {#AppNome} agora"; Flags: nowait postinstall skipifsilent

[Code]
{ Evita duplicar o diretório no PATH ao reinstalar. }
function NeedsAddPath(Param: string): Boolean;
var
  CaminhoAtual: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', CaminhoAtual) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(CaminhoAtual) + ';') = 0;
end;
