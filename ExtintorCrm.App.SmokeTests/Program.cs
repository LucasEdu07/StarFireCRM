using System;
using System.Collections.Generic;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.UseCases.Alerts;

namespace ExtintorCrm.App.SmokeTests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            SmokeTests.RunAll();
            Console.WriteLine("OK: 5/5 smoke tests passaram.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FALHA nos smoke tests:");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

internal static class AssertEx
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}. Esperado: {expected} | Atual: {actual}");
        }
    }
}

internal static class SmokeTests
{
    public static void RunAll()
    {
        AlertRules_SetAlertDays_SortsDistinctAndFiltersInvalid();
        AlertService_ApplyAlerts_Clientes_ComputaSituacao();
        AlertService_Counts_Clientes_ComputaVencidosEVencendo();
        AlertService_ApplyAlerts_Pagamentos_ComputaSituacao();
        AlertService_CountPagamentos_ConsideraApenasAbertos();
    }

    private static void AlertRules_SetAlertDays_SortsDistinctAndFiltersInvalid()
    {
        var rules = new AlertRules();
        rules.SetAlertDays(30, 7, 0, -2, 15, 7, 30);

        AssertEx.Equal(3, rules.AlertDays.Length, "AlertDays deve conter apenas positivos distintos");
        AssertEx.Equal(7, rules.AlertDays[0], "AlertDays deve estar ordenado");
        AssertEx.Equal(15, rules.AlertDays[1], "AlertDays deve estar ordenado");
        AssertEx.Equal(30, rules.AlertDays[2], "AlertDays deve estar ordenado");
        AssertEx.Equal(30, rules.MaxAlertDays, "MaxAlertDays incorreto");
    }

    private static void AlertService_ApplyAlerts_Clientes_ComputaSituacao()
    {
        var today = new DateTime(2026, 2, 23);
        var rules = new AlertRules();
        rules.SetAlertDays(7, 15, 30);
        var service = new AlertService(rules);

        var cliente = new Cliente
        {
            NomeFantasia = "Cliente Teste",
            VencimentoExtintores = today.AddDays(-1),
            VencimentoAlvara = today.AddDays(5)
        };

        service.ApplyAlerts(new[] { cliente }, today);

        AssertEx.Equal("Vencido", cliente.ExtintorStatus, "Status do extintor incorreto");
        AssertEx.Equal(-1, cliente.ExtintorDaysToDue ?? 0, "Dias do extintor incorreto");
        AssertEx.Equal("Vencendo", cliente.AlvaraStatus, "Status do alvará incorreto");
        AssertEx.Equal(5, cliente.AlvaraDaysToDue ?? -1, "Dias do alvará incorreto");
        AssertEx.Equal("Vencido", cliente.SituacaoNivel, "Situação geral incorreta");
        AssertEx.Equal("Vencido", cliente.SituacaoTexto, "Texto da situação geral incorreto");
    }

    private static void AlertService_Counts_Clientes_ComputaVencidosEVencendo()
    {
        var rules = new AlertRules();
        var service = new AlertService(rules);

        var clientes = new[]
        {
            new Cliente { ExtintorStatus = "Vencido", AlvaraStatus = "OK" },
            new Cliente { ExtintorStatus = "Vencendo", AlvaraStatus = "Vencendo" },
            new Cliente { ExtintorStatus = "OK", AlvaraStatus = "Vencido" }
        };

        var ext = service.CountExtintores(clientes);
        var alv = service.CountAlvaras(clientes);

        AssertEx.Equal(1, ext.Vencidos, "Contagem de extintores vencidos incorreta");
        AssertEx.Equal(1, ext.Vencendo, "Contagem de extintores vencendo incorreta");
        AssertEx.Equal(1, alv.Vencidos, "Contagem de alvarás vencidos incorreta");
        AssertEx.Equal(1, alv.Vencendo, "Contagem de alvarás vencendo incorreta");
    }

    private static void AlertService_ApplyAlerts_Pagamentos_ComputaSituacao()
    {
        var today = new DateTime(2026, 2, 23);
        var rules = new AlertRules();
        rules.SetAlertDays(30);
        var service = new AlertService(rules);

        var pagamentos = new[]
        {
            new Pagamento { Pago = true, DataVencimento = today.AddDays(-40) },
            new Pagamento { Pago = false, DataVencimento = today.AddDays(-2) },
            new Pagamento { Pago = false, DataVencimento = today.AddDays(10) },
            new Pagamento { Pago = false, DataVencimento = today.AddDays(45) }
        };

        service.ApplyAlerts(pagamentos, today);

        AssertEx.Equal("OK", pagamentos[0].SituacaoNivel, "Pagamento pago deve ficar OK");
        AssertEx.Equal("Pago", pagamentos[0].SituacaoTexto, "Pagamento pago deve ficar com texto Pago");

        AssertEx.Equal("Vencido", pagamentos[1].SituacaoNivel, "Pagamento vencido incorreto");
        AssertEx.Equal(-2, pagamentos[1].DaysToDue ?? 0, "DaysToDue vencido incorreto");

        AssertEx.Equal("Vencendo", pagamentos[2].SituacaoNivel, "Pagamento vencendo incorreto");
        AssertEx.Equal("Vence em 10 dias", pagamentos[2].SituacaoTexto, "Texto do pagamento vencendo incorreto");

        AssertEx.Equal("OK", pagamentos[3].SituacaoNivel, "Pagamento fora da janela deve ser OK");
    }

    private static void AlertService_CountPagamentos_ConsideraApenasAbertos()
    {
        var service = new AlertService(new AlertRules());
        var pagamentos = new[]
        {
            new Pagamento { Pago = false, SituacaoNivel = "Vencido" },
            new Pagamento { Pago = false, SituacaoNivel = "Vencendo" },
            new Pagamento { Pago = true, SituacaoNivel = "Vencido" }
        };

        var result = service.CountPagamentos(pagamentos);

        AssertEx.Equal(1, result.Vencidos, "CountPagamentos não deve incluir pagos em vencidos");
        AssertEx.Equal(1, result.Vencendo, "CountPagamentos vencendo incorreto");
    }
}
