# Backup e Restauração

Este guia explica como proteger os dados do cliente no **Star Fire CRM**.

## Onde os dados ficam

- Banco e configurações: `%LocalAppData%\StarFire\data`
- Backups padrão: `%LocalAppData%\StarFire\data\backups`

## Configurar backup automático

1. Abra **Configurações**.
2. Entre na seção **Backup**.
3. Defina:
   - pasta de backup
   - intervalo automático
   - retenção (quantidade de arquivos)
4. Clique em **Salvar configurações**.

## Fazer backup manual

1. Abra **Configurações > Backup**.
2. Clique no botão de **Backup**.
3. Aguarde a confirmação na notificação (toast).

## Restaurar backup

1. Feche operações em andamento.
2. Abra **Configurações > Backup**.
3. Clique em **Restaurar backup**.
4. Escolha o arquivo de backup.
5. Confirme a operação.

## Recomendações

- Faça backup antes de importações grandes.
- Mantenha cópia externa (pendrive/nuvem corporativa).
- Não exclua manualmente a pasta `%LocalAppData%\StarFire\data`.

## Problemas comuns

### Acesso negado ao restaurar

- Feche o app e tente novamente.
- Verifique permissões da pasta de destino.
- Evite restaurar de arquivos abertos por outro programa.

### Backup não aparece

- Confirme a pasta configurada.
- Verifique se o usuário tem permissão de escrita.

