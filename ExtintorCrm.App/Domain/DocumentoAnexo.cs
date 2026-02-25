using System;

namespace ExtintorCrm.App.Domain
{
    public class DocumentoAnexo
    {
        public Guid Id { get; set; }
        public Guid? ClienteId { get; set; }
        public Guid? PagamentoId { get; set; }
        public string Contexto { get; set; } = "Pagamento";
        public string TipoDocumento { get; set; } = "Outro";
        public string NomeOriginal { get; set; } = string.Empty;
        public string CaminhoRelativo { get; set; } = string.Empty;
        public long TamanhoBytes { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }
}
