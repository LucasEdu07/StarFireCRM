# Testes Smoke (Offline)

Este projeto possui um runner de testes sem dependências externas:

- Projeto: `ExtintorCrm.App.SmokeTests`
- Comando:

```powershell
dotnet run --project .\ExtintorCrm.App.SmokeTests\
```

Saída esperada:

```text
OK: 5/5 smoke tests passaram.
```

Os testes cobrem:

- Regras de alerta (`AlertRules`)
- Cálculo de situação para clientes e pagamentos (`AlertService`)
- Contadores de vencidos/vencendo
