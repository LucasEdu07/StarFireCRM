# Star Fire CRM - Release Playbook

Playbook oficial para gerar nova versao offline com seguranca.

## 1) Pre-requisitos

- .NET SDK 8 instalado
- Inno Setup 6 instalado (com `iscc.exe`)
- projeto compilando localmente

## 2) Pontos de versao que devem ser atualizados

1. Versao do app em `ExtintorCrm.App/ExtintorCrm.App.csproj` (`<Version>`).
2. Historico de release na UI em `ExtintorCrm.App/Presentation/ClientesViewModel.Utilities.partial.cs` (metodo `BuildReleaseNotesHistory`).
3. Comandos de publish usando a mesma versao (`-Version <versao>`).

## 3) Checklist pre-release

1. Confirmar branch e mudancas finais (`git status`).
2. Build da aplicacao:

```powershell
dotnet build .\ExtintorCrm.App\ExtintorCrm.App.csproj
```

3. Build e execucao de smoke tests:

```powershell
dotnet build .\ExtintorCrm.App.SmokeTests\ExtintorCrm.App.SmokeTests.csproj --no-restore
dotnet run --project .\ExtintorCrm.App.SmokeTests\ --no-build
```

4. Validacao funcional rapida:

- Dashboard, Clientes, Pagamentos, Configuracoes
- fluxo de backup/restore
- tema Light/Dark

## 4) Gerar publish

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version <versao>
```

Saida:

- `artifacts\publish\StarFire-<versao>\`

## 5) Gerar instalador

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version <versao> -BuildInstaller
```

Saida:

- `artifacts\installer\StarFire-Setup-<versao>.exe`

## 6) Validacao do instalador

1. Instalar uma versao anterior.
2. Cadastrar dado de teste.
3. Rodar `StarFire-Setup-<versao>.exe`.
4. Validar:

- dados preservados
- app abre normalmente
- versao correta em **Configuracoes > Sobre**

## 7) Entrega ao cliente

Enviar somente:

- `StarFire-Setup-<versao>.exe`

Instrucao curta:

1. Fechar o app.
2. Executar o instalador.
3. Avancar ate concluir.
4. Reabrir o sistema.

## 8) Rollback (se necessario)

1. Reinstalar versao anterior.
2. Restaurar backup em `%LocalAppData%\StarFire\data\backups`.

## 9) Problemas comuns

- erro de restore/publish por acesso NuGet/rede
- app aberto durante update
- ausencia de Inno Setup na maquina
