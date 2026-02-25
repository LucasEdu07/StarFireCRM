using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.UseCases
{
    public interface IDocumentoAnexoRepository
    {
        Task<List<DocumentoAnexo>> ListByPagamentoAsync(Guid pagamentoId);
        Task<List<DocumentoAnexo>> ListByClienteAlvaraAsync(Guid clienteId);
        Task<DocumentoAnexo?> GetByIdAsync(Guid id);
        Task AddAsync(DocumentoAnexo documentoAnexo);
        Task DeleteAsync(Guid id);
    }
}
