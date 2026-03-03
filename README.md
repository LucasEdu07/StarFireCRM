# Star Fire CRM

Aplicacao desktop WPF para gestao operacional offline de clientes, vencimentos e pagamentos.

## Visao geral

O **Star Fire CRM** foi projetado para operacao local com foco em:

- cadastro e gestao de clientes
- controle de vencimentos de extintores e alvaras
- gestao de pagamentos
- alertas e pendencias criticas
- backup/restauracao e importacao/exportacao

## Tecnologias

- .NET 8 (WPF / C#)
- EF Core 8 + SQLite
- ClosedXML (arquivos Excel)
- Inno Setup (instalador)

## Requisitos de desenvolvimento

- Windows 10/11
- .NET SDK 8.0
- Inno Setup 6 (para gerar instalador)

## Estrutura principal

```text
ExtintorCrm.App/
  Domain/
  Infrastructure/
  Migrations/
  Presentation/
  UseCases/
ExtintorCrm.App.SmokeTests/
scripts/
installer/
docs/
```

## Como executar localmente

```powershell
dotnet restore
dotnet build
dotnet run --project .\ExtintorCrm.App\
```

## Qualidade local (recomendado antes de commit)

```powershell
dotnet build .\ExtintorCrm.App\ExtintorCrm.App.csproj --no-restore
dotnet build .\ExtintorCrm.App.SmokeTests\ExtintorCrm.App.SmokeTests.csproj --no-restore
dotnet run --project .\ExtintorCrm.App.SmokeTests\ --no-build
git status --short
```

Checklist rapido:

- confirmar que `bin/` e `obj/` nao entraram no commit
- separar commits por tema
- garantir build e smoke verdes

## Publicacao e instalador

Publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version <versao>
```

Publish + instalador:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version <versao> -BuildInstaller
```

Saidas:

- publish: `artifacts/publish/StarFire-<versao>/`
- instalador: `artifacts/installer/StarFire-Setup-<versao>.exe`

## Ralph Loop local (Codex)

Loop iterativo de validacao com build + smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Ralph-Loop.ps1 -MaxCycles 5
```

Modo manual entre ciclos:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Ralph-Loop.ps1 -MaxCycles 5 -PromptBetweenCycles
```

## Documentacao

Indice oficial: `docs/INDEX.md`

Principais guias:

- operacao: `docs/OPERACAO_CLIENTE.md`
- arquitetura: `docs/ARQUITETURA.md`
- release: `docs/RELEASE_PLAYBOOK.md`
- smoke tests: `docs/TESTES_SMOKE.md`
- QA fluxos criticos: `docs/QA_FLUXOS_CRITICOS.md`
