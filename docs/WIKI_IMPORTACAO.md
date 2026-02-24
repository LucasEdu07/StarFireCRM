# Importação de Dados

Guia rápido para importar clientes e pagamentos com segurança no **Star Fire CRM**.

## Antes de importar

Checklist:

1. Faça backup manual.
2. Valide se os cabeçalhos da planilha estão corretos.
3. Remova linhas totalmente vazias.
4. Confirme CPF/CNPJ dos clientes.

## Importação de Clientes

1. Abra a aba **Clientes**.
2. Clique em **Importar**.
3. Selecione a planilha.
4. Aguarde o resumo final:
   - inseridos
   - atualizados
   - ignorados (com motivo)

## Importação de Pagamentos

1. Abra a aba **Pagamentos**.
2. Clique em **Importar**.
3. Selecione a planilha.
4. Verifique o vínculo com cliente existente.

Observação: se o cliente não existir no sistema, o pagamento pode ser ignorado.

## Motivos comuns de linha ignorada

- linha em branco
- nome do cliente vazio
- CPF/CNPJ inválido ou ausente
- data inválida
- cliente não encontrado (no caso de pagamentos)

## Pós-importação (conferência)

1. Verifique os KPIs no Dashboard.
2. Abra cards de aviso para conferência de vencimentos.
3. Revise amostras na aba **Clientes** e **Pagamentos**.

## Boas práticas

- Use um padrão único de planilha para a operação.
- Evite alterar nome das colunas principais.
- Faça importações por lotes quando o volume for muito grande.

