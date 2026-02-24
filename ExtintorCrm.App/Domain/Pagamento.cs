using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExtintorCrm.App.Domain
{
    public class Pagamento
    {
        public Guid Id { get; set; }
        public Guid ClienteId { get; set; }
        public string? CpfCnpjCliente { get; set; }
        public string? Tipo { get; set; }
        public string? Status { get; set; }
        public DateTime? DataPrevista { get; set; }
        public DateTime? DataEfetiva { get; set; }
        public DateTime? VencimentoFatura { get; set; }
        public decimal? ValorPrevisto { get; set; }
        public decimal? ValorEfetivo { get; set; }
        public string? Categoria { get; set; }
        public string? Subcategoria { get; set; }
        public string? Conta { get; set; }
        public string? ContaTransferencia { get; set; }
        public string? Centro { get; set; }
        public string? Contato { get; set; }
        public string? RazaoSocial { get; set; }
        public string? Forma { get; set; }
        public string? Projeto { get; set; }
        public string? NumeroDocumento { get; set; }
        public string? Observacoes { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public DateTime DataVencimento { get; set; }
        public bool Pago { get; set; }
        public DateTime? DataPagamento { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }

        [NotMapped]
        public string? ClienteNome { get; set; }

        [NotMapped]
        public string SituacaoNivel { get; set; } = "OK";

        [NotMapped]
        public string SituacaoTexto { get; set; } = "OK";

        [NotMapped]
        public int? DaysToDue { get; set; }
    }
}
