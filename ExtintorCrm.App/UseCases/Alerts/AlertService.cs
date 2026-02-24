using System;
using System.Collections.Generic;
using System.Linq;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.UseCases.Alerts
{
    public class AlertService
    {
        private readonly AlertRules _rules;

        public AlertService(AlertRules rules)
        {
            _rules = rules;
        }

        public void ApplyAlerts(IEnumerable<Cliente> clientes, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            foreach (var cliente in clientes)
            {
                var ext = GetStatus(cliente.VencimentoExtintores, today);
                var alv = GetStatus(cliente.VencimentoAlvara, today);

                cliente.ExtintorStatus = ext.Label;
                cliente.ExtintorDaysToDue = ext.DaysToDue;
                cliente.AlvaraStatus = alv.Label;
                cliente.AlvaraDaysToDue = alv.DaysToDue;
                cliente.SituacaoNivel = ResolveOverallStatus(ext.Label, alv.Label);
                cliente.SituacaoTexto = BuildSituacaoTexto(cliente.SituacaoNivel, ext.DaysToDue, alv.DaysToDue);
            }
        }

        public (int Vencidos, int Vencendo) CountExtintores(IEnumerable<Cliente> clientes)
        {
            return CountByStatus(clientes.Select(c => c.ExtintorStatus));
        }

        public (int Vencidos, int Vencendo) CountAlvaras(IEnumerable<Cliente> clientes)
        {
            return CountByStatus(clientes.Select(c => c.AlvaraStatus));
        }

        public void ApplyAlerts(IEnumerable<Pagamento> pagamentos, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            foreach (var pagamento in pagamentos)
            {
                if (pagamento.Pago)
                {
                    pagamento.SituacaoNivel = "OK";
                    pagamento.SituacaoTexto = "Pago";
                    pagamento.DaysToDue = null;
                    continue;
                }

                var status = GetStatus(pagamento.DataVencimento, today);
                pagamento.DaysToDue = status.DaysToDue;
                pagamento.SituacaoNivel = status.Label;
                pagamento.SituacaoTexto = status.Label switch
                {
                    "Vencido" => "Vencido",
                    "Vencendo" => $"Vence em {status.DaysToDue ?? 0} dias",
                    _ => "OK"
                };
            }
        }

        public (int Vencidos, int Vencendo) CountPagamentos(IEnumerable<Pagamento> pagamentos)
        {
            var abertos = pagamentos.Where(p => !p.Pago);
            return CountByStatus(abertos.Select(p => p.SituacaoNivel));
        }

        private (string Label, int? DaysToDue) GetStatus(DateTime? dueDate, DateTime today)
        {
            if (!dueDate.HasValue)
            {
                return ("OK", null);
            }

            var days = (dueDate.Value.Date - today).Days;
            if (days < 0)
            {
                return ("Vencido", days);
            }

            if (days <= _rules.MaxAlertDays)
            {
                return ("Vencendo", days);
            }

            return ("OK", days);
        }

        private static (int Vencidos, int Vencendo) CountByStatus(IEnumerable<string?> statuses)
        {
            var vencidos = statuses.Count(s => string.Equals(s, "Vencido", StringComparison.OrdinalIgnoreCase));
            var vencendo = statuses.Count(s => string.Equals(s, "Vencendo", StringComparison.OrdinalIgnoreCase));
            return (vencidos, vencendo);
        }

        private static string ResolveOverallStatus(string ext, string alv)
        {
            if (ext == "Vencido" || alv == "Vencido")
            {
                return "Vencido";
            }

            if (ext == "Vencendo" || alv == "Vencendo")
            {
                return "Vencendo";
            }

            return "OK";
        }

        private static string BuildSituacaoTexto(string status, int? extDays, int? alvDays)
        {
            if (status == "Vencido")
            {
                return "Vencido";
            }

            if (status == "Vencendo")
            {
                var days = new[] { extDays, alvDays }
                    .Where(x => x.HasValue && x.Value >= 0)
                    .Select(x => x!.Value)
                    .DefaultIfEmpty(0)
                    .Min();
                return $"Vence em {days} dias";
            }

            return "OK";
        }
    }
}
