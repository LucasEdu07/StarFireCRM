# Atualização de Versão

Como atualizar o **Star Fire CRM** sem perder os dados atuais.

## Resumo

- O app é atualizado pelo instalador (`StarFire-Setup-<versão>.exe`).
- Os dados do cliente ficam em pasta local separada.
- Atualizar o app **não** deve apagar o banco de dados.

## Estrutura de pastas

- Aplicativo: `C:\Program Files\Star Fire`
- Dados: `%LocalAppData%\StarFire\data`

## Passo a passo (cliente final)

1. Feche o Star Fire CRM.
2. Execute `StarFire-Setup-<versão>.exe`.
3. Clique em **Avançar** até finalizar.
4. Abra o app e valide a nova versão em **Configurações > Sobre**.

## Antes de atualizar (recomendado)

1. Gere um backup manual.
2. Garanta que ninguém está usando o app no momento.

## Após atualizar

1. Verifique clientes e pagamentos.
2. Abra Dashboard e confira KPIs.
3. Teste uma ação de rotina (ex.: abrir perfil de cliente).

## Se algo der errado

1. Não apague a pasta `%LocalAppData%\StarFire\data`.
2. Abra **Configurações > Backup** e restaure um backup válido.
3. Acione suporte por **Configurações > Sobre**.

