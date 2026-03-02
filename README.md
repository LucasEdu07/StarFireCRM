# Star Fire CRM

Aplicacao desktop WPF para gestao operacional de clientes, vencimentos, alertas e pagamentos.

## Visao Geral

O **Star Fire CRM** foi projetado para operacao offline com foco em:

- Cadastro e gestao de clientes
- Controle de vencimento de extintores e alvaras
- Gestao de pagamentos
- Alertas e pendencias criticas
- Backup e restauracao
- Importacao/Exportacao de dados

## Principais Funcionalidades

- Dashboard executivo com KPIs e alertas
- CRUD completo de clientes
- Perfil detalhado do cliente (extintores, alvara, pagamentos, observacoes)
- CRUD de pagamentos
- Importacao de clientes por planilha
- Importacao de pagamentos por planilha
- Exportacao para Excel/CSV com selecao de campos
- Tema Light/Dark com persistencia
- Backup manual/automatico e restauracao
- Atualizacao de versao via instalador

## Tecnologias

- .NET 8 (WPF)
- EF Core 8 + SQLite
- ClosedXML (Excel)
- Inno Setup (instalador)

## Requisitos

- Windows 10/11
- .NET SDK 8.0 (para desenvolvimento)
- Microsoft Excel instalado (recomendado para alguns fluxos de importacao de arquivos legados)

## Estrutura do Projeto

```text
ExtintorCrm.App/
  Domain/
  Infrastructure/
  Migrations/
  Presentation/
  UseCases/
scripts/
installer/
docs/
```

## Como Executar

```powershell
dotnet restore
dotnet build
dotnet run --project .\ExtintorCrm.App\
```

## Build e Publicacao

Gerar publish + instalador:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version 1.0.4 -BuildInstaller
```

Saidas:

- Publish: `artifacts/publish/StarFire-<versao>/`
- Instalador: `artifacts/installer/StarFire-Setup-<versao>.exe`

## Dados, Backup e Atualizacao

- O app utiliza pasta de dados local do usuario (nao sobrescrever dados em atualizacao de executavel).
- Use a aba **Configuracoes > Backup** para:
  - definir pasta de backup
  - configurar backup automatico
  - restaurar backup

Leia tambem:

- `docs/ATUALIZACAO-CLIENTE.md`
- `README_RELEASE.md`

## Importacao

### Clientes

- Suporta planilhas e CSV
- Regras de validacao aplicadas no importador
- Linhas invalidas sao ignoradas com motivo no resumo

### Pagamentos

- Importacao por aba de Pagamentos
- Validacao por vinculo com cliente existente

## Testes e Smoke

Referencia de validacao manual:

- `docs/TESTES_SMOKE.md`
- `docs/QA_FLUXOS_CRITICOS.md`
- projeto de apoio: `ExtintorCrm.App.SmokeTests`

## Fluxo Pre-Commit (Recomendado)

Antes de subir alteracoes:

```powershell
dotnet build .\ExtintorCrm.App\ExtintorCrm.App.csproj --no-restore
dotnet build .\ExtintorCrm.App.SmokeTests\ExtintorCrm.App.SmokeTests.csproj --no-restore
dotnet run --project .\ExtintorCrm.App.SmokeTests\ --no-build
git status --short
```

Checklist rapido:

- Confirmar que `bin/` e `obj/` nao entraram no commit
- Separar commits por tema (ex.: UX, bugfix, higiene)
- Garantir que build e smoke continuam verdes

## Ralph Loop Local (Codex)

Para rodar um loop iterativo de validacao (estilo "ralph loop") com build + smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Ralph-Loop.ps1 -MaxCycles 5
```

Modo manual entre ciclos (pausa para voce ajustar no Codex e continuar):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Ralph-Loop.ps1 -MaxCycles 5 -PromptBetweenCycles
```

Com comandos customizados de correcao entre ciclos:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Ralph-Loop.ps1 `
  -MaxCycles 3 `
  -FixCommands "dotnet format .\ExtintorCrm.sln --verify-no-changes"
```

## Suporte

No app, acesse **Configuracoes > Sobre** para acionar suporte por WhatsApp ou e-mail com mensagem pre-preenchida.
