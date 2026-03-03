# Star Fire CRM - Arquitetura

Este documento resume a arquitetura atual do produto e as decisoes de engenharia adotadas.

## 1) Visao geral

Aplicacao desktop WPF offline-first, com persistencia local em SQLite e distribuicao por instalador.

Fluxo de alto nivel:

`WPF UI (Presentation) -> UseCases/Servicos -> Domain -> Infrastructure (EF Core + SQLite)`

## 2) Camadas e responsabilidades

### `ExtintorCrm.App/Presentation`

- janelas, estilos e experiencia de uso (WPF/XAML)
- view models, comandos e interacoes com o usuario
- temas Light/Dark e componentes visuais

### `ExtintorCrm.App/UseCases`

- casos de uso e orquestracao de fluxos
- regras de aplicacao e contratos de retorno/validacao

### `ExtintorCrm.App/Domain`

- entidades e regras de negocio centrais
- invariantes e modelos do dominio

### `ExtintorCrm.App/Infrastructure`

- persistencia via EF Core + SQLite
- repositorios, migracoes, backup/restore e importadores
- integracoes locais necessarias para operacao

## 3) Persistencia e dados

- banco local: SQLite
- dados do usuario: `%LocalAppData%\\StarFire\\data`
- atualizacao por instalador nao deve sobrescrever dados do usuario

## 4) Distribuicao

- publish via `scripts/Publish-StarFire.ps1`
- instalador via Inno Setup (`installer/StarFire.iss`)
- artefatos em `artifacts/publish` e `artifacts/installer`

## 5) Qualidade e validacao

- build local da aplicacao e projeto smoke
- smoke tests em `ExtintorCrm.App.SmokeTests`
- checklist funcional em `docs/QA_FLUXOS_CRITICOS.md`

## 6) Decisoes arquiteturais

Decisoes principais:

- desktop local em vez de web/mobile (contexto de cliente medio porte)
- monolito organizado por camadas para reduzir custo operacional
- offline-first para confiabilidade da operacao diaria

Trade-offs aceitos:

- menor elasticidade horizontal comparado a arquitetura distribuida
- dependencia de instalador para entrega de versoes

## 7) Diretrizes para evolucao

- preservar separacao de responsabilidades entre camadas
- evitar regra de negocio em camada de UI
- manter consistencia visual com componentes/estilos compartilhados
- validar mudancas com build + smoke antes de release
