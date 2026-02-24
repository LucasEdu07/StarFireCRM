using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.UseCases
{
    public interface IClienteRepository
    {
        Task<List<Cliente>> GetAllAsync();
        Task<List<Cliente>> SearchAsync(string term);
        Task<Cliente?> GetByIdAsync(Guid id);
        Task<bool> ExistsByCpfAsync(string cpfCnpjDigits, Guid? excludeId = null);
        Task AddAsync(Cliente cliente);
        Task UpdateAsync(Cliente cliente);
        Task DeleteAsync(Guid id);
    }
}
