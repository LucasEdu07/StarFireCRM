# Testes Smoke (Offline)

Este projeto possui um runner de testes sem dependencias externas:

- Projeto: `ExtintorCrm.App.SmokeTests`
- Comando:

```powershell
dotnet run --project .\ExtintorCrm.App.SmokeTests\
```

Saida esperada:

```text
OK: 12/12 smoke tests passaram.
```

Os testes cobrem:

- Regras de alerta (`AlertRules`)
- Calculo de situacao para clientes e pagamentos (`AlertService`)
- Contadores de vencidos/vencendo
- Contratos de validacao (`ValidationResult`)
- Contratos de operacao (`OperationResult`)
- Resultado agregado de importacao (`ImportResult`)
- Validacao de arquivo/cabecalho na importacao (`ImportValidation`)
- Erros de pre-condicao em backup e restore (`BackupService`)
