# Star Fire CRM - Guia de Operacao

Guia oficial para uso diario do produto em ambiente desktop local/offline.

## 1) Escopo do sistema

O Star Fire CRM cobre a rotina operacional de:

- cadastro e atualizacao de clientes
- controle de vencimentos (extintores e alvaras)
- gestao de pagamentos
- acompanhamento por dashboard e alertas
- backup, restauracao, importacao e exportacao

## 2) Requisitos minimos

- Windows 10 ou 11
- instalador oficial `StarFire-Setup-<versao>.exe`

## 3) Rotina recomendada (dia a dia)

1. Abrir o **Dashboard** e revisar KPIs/alertas.
2. Entrar em **Clientes** para cadastros, ajustes e consultas.
3. Entrar em **Pagamentos** para registrar e acompanhar pendencias.
4. Encerrar com backup (manual ou automatico configurado).

## 4) Operacao em Clientes

- Busca por nome, documento e telefone.
- Cadastro e edicao com validacoes de campos obrigatorios.
- Filtros de status e ordenacao para priorizacao.
- Importacao/exportacao para trabalho em lote.

## 5) Operacao em Pagamentos

- Registro de pagamento vinculado a cliente existente.
- Controle de vencimentos e situacao (aberto/pago).
- Importacao de planilha com validacao de cabecalhos e vinculo.

## 6) Importacao de dados (boas praticas)

Antes de importar:

1. Fazer backup manual.
2. Validar cabecalhos do arquivo.
3. Remover linhas totalmente vazias.
4. Conferir CPF/CNPJ quando aplicavel.

Após importar:

- revisar resumo de inseridos/atualizados/ignorados
- conferir amostra de registros em Clientes e Pagamentos
- revisar alertas no Dashboard

## 7) Backup e restauracao

Configuracao em **Configuracoes > Backup**:

- definir pasta de backup
- configurar backup automatico
- executar backup manual
- restaurar backup por arquivo

Pastas padrao:

- dados do app: `%LocalAppData%\\StarFire\\data`
- backups: `%LocalAppData%\\StarFire\\data\\backups`

## 8) Atualizacao de versao sem perder dados

- Aplicativo: `C:\\Program Files\\Star Fire`
- Dados: `%LocalAppData%\\StarFire\\data`

Passos:

1. Fechar o app.
2. Executar `StarFire-Setup-<versao>.exe`.
3. Concluir instalacao.
4. Abrir o app e validar em **Configuracoes > Sobre**.

## 9) Boas praticas operacionais

- Nao apagar manualmente `%LocalAppData%\\StarFire\\data`.
- Realizar backup antes de importacoes grandes.
- Usar sempre instalador oficial para atualizacao.

## 10) Suporte

No app, acessar **Configuracoes > Sobre** para contato por WhatsApp ou e-mail.
