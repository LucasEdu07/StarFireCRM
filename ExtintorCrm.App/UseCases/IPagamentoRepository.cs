using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.UseCases
{
    public interface IPagamentoRepository
    {
        Task<List<Pagamento>> GetAllAsync();
        Task<Pagamento?> GetByIdAsync(Guid id);
        Task AddAsync(Pagamento pagamento);
        Task UpdateAsync(Pagamento pagamento);
        Task DeleteAsync(Guid id);
    }
}
