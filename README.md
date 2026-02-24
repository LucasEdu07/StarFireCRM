# Star Fire CRM

Aplicação desktop WPF para gestão operacional de clientes, vencimentos, alertas e pagamentos.

## Visão Geral

O **Star Fire CRM** foi projetado para operação offline com foco em:

- Cadastro e gestão de clientes
- Controle de vencimento de extintores e alvarás
- Gestão de pagamentos
- Alertas e pendências críticas
- Backup e restauração
- Importação/Exportação de dados

## Principais Funcionalidades

- Dashboard executivo com KPIs e alertas
- CRUD completo de clientes
- Perfil detalhado do cliente (extintores, alvará, pagamentos, observações)
- CRUD de pagamentos
- Importação de clientes por planilha
- Importação de pagamentos por planilha
- Exportação para Excel/CSV com seleção de campos
- Tema Light/Dark com persistência
- Backup manual/automático e restauração
- Atualização de versão via instalador

## Tecnologias

- .NET 8 (WPF)
- EF Core 8 + SQLite
- ClosedXML (Excel)
- Inno Setup (instalador)

## Requisitos

- Windows 10/11
- .NET SDK 8.0 (para desenvolvimento)
- Microsoft Excel instalado (recomendado para alguns fluxos de importação de arquivos legados)

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

## Build e Publicação

Gerar publish + instalador:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-StarFire.ps1 -Version 1.0.4 -BuildInstaller
```

Saídas:

- Publish: `artifacts/publish/StarFire-<versão>/`
- Instalador: `artifacts/installer/StarFire-Setup-<versão>.exe`

## Dados, Backup e Atualização

- O app utiliza pasta de dados local do usuário (não sobrescrever dados em atualização de executável).
- Use a aba **Configurações > Backup** para:
  - definir pasta de backup
  - configurar backup automático
  - restaurar backup

Leia também:

- `docs/ATUALIZACAO-CLIENTE.md`
- `README_RELEASE.md`

## Importação

### Clientes

- Suporta planilhas e CSV
- Regras de validação aplicadas no importador
- Linhas inválidas são ignoradas com motivo no resumo

### Pagamentos

- Importação por aba de Pagamentos
- Validação por vínculo com cliente existente

## Testes e Smoke

Referência de validação manual:

- `docs/TESTES_SMOKE.md`
- projeto de apoio: `ExtintorCrm.App.SmokeTests`

## Suporte

No app, acesse **Configurações > Sobre** para acionar suporte por WhatsApp ou e-mail com mensagem pré-preenchida.

