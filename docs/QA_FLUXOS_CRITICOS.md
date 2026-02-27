# QA Fluxos Criticos

Checklist de aceite funcional e UX para os 8 fluxos centrais do Star Fire CRM.

## 1) Cadastrar cliente
- Nome obrigatorio validado antes de salvar.
- CPF/CNPJ obrigatorio e sem duplicidade.
- Mensagem final informa resultado e proximo passo.

## 2) Editar cliente
- Alteracoes persistem apos recarregar lista.
- Validacoes de campo seguem as mesmas regras do cadastro.
- Falhas mostram motivo tecnico e orientacao de correcao.

## 3) Importar clientes
- Arquivo invalido gera erro acionavel.
- Cabecalhos obrigatorios ausentes sao reportados.
- Resumo final exibe inseridos, atualizados e ignorados.
- Motivos de linhas ignoradas aparecem agrupados.

## 4) Cadastrar pagamento
- Nao permite salvar sem cliente vinculado.
- Valor e vencimento obrigatorios.
- Status final e feedback visivel na listagem.

## 5) Importar pagamentos
- Cabecalhos obrigatorios (`cpfcnpj`, `descricao`) validados.
- Linhas sem cliente correspondente sao sinalizadas.
- Resumo final exibe contagens e pendencias de importacao.

## 6) Backup manual
- Sem pasta configurada: erro com proximo passo.
- Com pasta valida: backup finaliza com sucesso.
- Arquivo gerado pode ser localizado pela equipe.

## 7) Restaurar backup
- Confirmacao explicita impacto da restauracao.
- Arquivo invalido retorna erro orientativo.
- Restauracao valida recarrega dados e confirma resultado.

## 8) Atualizacao por instalador
- Nome de arquivo invalido bloqueia execucao.
- Confirmacao antes de fechar o app.
- Falha para abrir instalador exibe erro claro.

