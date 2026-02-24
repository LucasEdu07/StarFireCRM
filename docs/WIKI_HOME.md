# Star Fire CRM

Guia oficial de operação do **Star Fire CRM** para uso diário, atualização e suporte.

## Visão rápida

O Star Fire CRM é um sistema desktop (offline) para:

- gestão de clientes (ativos/inativos)
- vencimentos de extintores e alvarás
- controle de pagamentos
- alertas e pendências operacionais
- backup, restauração e atualização de versão

## Requisitos

- Windows 10 ou 11
- Instalador oficial `StarFire-Setup-<versão>.exe`

## Primeiros passos

1. Instale o aplicativo pelo instalador oficial.
2. Abra o sistema e valide as abas:
   - Dashboard
   - Clientes
   - Pagamentos
   - Configurações
3. Configure o tema (Light/Dark) e backup em **Configurações**.

## Fluxo recomendado (operação diária)

1. Verifique os KPIs e avisos no **Dashboard**.
2. Clique nos cards de aviso para abrir a conferência de clientes afetados.
3. Entre em **Clientes** para:
   - abrir perfil do cliente
   - editar dados e vencimentos
   - importar/exportar base
4. Entre em **Pagamentos** para:
   - lançar pagamentos
   - editar status (aberto/pago)
   - acompanhar vencimentos

## Importação de dados

### Clientes

- Use a ação **Importar** na aba **Clientes**.
- O importador valida colunas e ignora linhas inválidas com resumo de motivos.
- Evite planilhas com colunas renomeadas fora do padrão.

### Pagamentos

- Use a ação **Importar** na aba **Pagamentos**.
- O cliente precisa existir no sistema para vínculo correto.

## Backup e restauração

Em **Configurações > Backup** você pode:

- selecionar pasta de backup
- configurar backup automático
- criar backup manual
- restaurar backup

## Atualização de versão (sem perder dados)

### Como funciona

- Aplicativo: `C:\Program Files\Star Fire`
- Dados: `%LocalAppData%\StarFire\data`

Ao atualizar o executável, os dados do cliente continuam preservados.

### Passo a passo (cliente final)

1. Feche o Star Fire CRM.
2. Execute `StarFire-Setup-<versão>.exe`.
3. Avance até concluir a instalação.
4. Abra o sistema novamente.

## Boas práticas

- Não apagar manualmente `%LocalAppData%\StarFire\data`.
- Executar backup antes de importações grandes.
- Atualizar apenas com instalador oficial.

## Suporte

No aplicativo, abra **Configurações > Sobre** para acionar:

- WhatsApp de suporte
- E-mail de suporte

---

## Como publicar este conteúdo na Wiki do GitHub

Se quiser usar este arquivo como Home da Wiki:

1. Abra a Wiki do repositório:
   - `https://github.com/LucasEdu07/StarFireCRM/wiki`
2. Clique em **Edit** na página Home.
3. Cole o conteúdo deste arquivo e salve.

