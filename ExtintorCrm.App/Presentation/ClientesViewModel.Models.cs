using System;

namespace ExtintorCrm.App.Presentation
{
    public class CriticalAlertItem
    {
        public string Cliente { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int Dias { get; set; }
        public string Nivel { get; set; } = "OK";

        public string DiasTexto
        {
            get => Nivel == "Vencido"
                ? $"Vencido há {Math.Abs(Dias)} dia(s)"
                : $"Vence em {Dias} dia(s)";
            set { }
        }
    }

    public class DashboardAlertItem
    {
        public Guid ClienteId { get; set; }
        public string ClienteNome { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public DateTime? DataVencimento { get; set; }
        public int? DiasParaVencer { get; set; }
        public string Status { get; set; } = "OK";

        public string DataVencimentoTexto => DataVencimento.HasValue ? DataVencimento.Value.ToString("dd/MM/yyyy") : "—";
        public string DiasTexto => DiasParaVencer.HasValue ? DiasParaVencer.Value.ToString() : "—";
        public string SituacaoNivel => Status;
        public string SituacaoTexto => Status == "Vencido"
            ? "Vencido"
            : Status == "Vencendo"
                ? "Vencendo"
                : "OK";
    }
}
