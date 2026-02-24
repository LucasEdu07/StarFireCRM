using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExtintorCrm.App.Domain
{
    public class Cliente
    {
        public Guid Id { get; set; }

        public string NomeFantasia { get; set; } = string.Empty;

        public string? RazaoSocial { get; set; }
        public string? Documento { get; set; }
        public string? RG { get; set; }
        public string? CPF { get; set; }
        public DateTime? Nascimento { get; set; }
        public string? Sexo { get; set; }
        public string? Categoria { get; set; }
        public string? Contato { get; set; }
        public string? TipoContato { get; set; }
        public string? Telefone { get; set; }
        public string? Telefone1 { get; set; }
        public string? Telefone2 { get; set; }
        public string? Telefone3 { get; set; }
        public string? Email { get; set; }
        public string? Endereco { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? UF { get; set; }
        public string? CEP { get; set; }
        public string? Observacoes { get; set; }

        public string? TipoServico { get; set; }
        public string? StatusRecarga { get; set; }
        public DateTime? VencimentoServico { get; set; }
        public DateTime? VencimentoExtintores { get; set; }

        public string? NumeroAlvara { get; set; }
        public DateTime? VencimentoAlvara { get; set; }
        public string? Representante { get; set; }
        public bool IsAtivo { get; set; } = true;
        public string? Status { get; set; }
        public bool AvisoAtivo { get; set; }

        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }

        [NotMapped]
        public string ExtintorStatus { get; set; } = "OK";

        [NotMapped]
        public int? ExtintorDaysToDue { get; set; }

        [NotMapped]
        public string AlvaraStatus { get; set; } = "OK";

        [NotMapped]
        public int? AlvaraDaysToDue { get; set; }

        [NotMapped]
        public string SituacaoNivel { get; set; } = "OK";

        [NotMapped]
        public string SituacaoTexto { get; set; } = "OK";
    }
}
