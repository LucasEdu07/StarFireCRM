using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ExtintorCrm.App.UseCases;
using ExtintorCrm.App.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure
{
    public class ClienteRepository : IClienteRepository
    {
        public async Task<List<Cliente>> GetAllAsync()
        {
            await using var db = new AppDbContext();
            return await db.Clientes.AsNoTracking().ToListAsync();
        }

        public async Task<List<Cliente>> SearchAsync(string term)
        {
            await using var db = new AppDbContext();
            var query = db.Clientes.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                query = query.Where(x =>
                    (x.NomeFantasia != null && x.NomeFantasia.Contains(term)) ||
                    (x.Documento != null && x.Documento.Contains(term)) ||
                    (x.CPF != null && x.CPF.Contains(term)) ||
                    (x.Telefone != null && x.Telefone.Contains(term)) ||
                    (x.Telefone1 != null && x.Telefone1.Contains(term)));
            }

            return await query.ToListAsync();
        }

        public async Task<Cliente?> GetByIdAsync(Guid id)
        {
            await using var db = new AppDbContext();
            return await db.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<bool> ExistsByCpfAsync(string cpfCnpjDigits, Guid? excludeId = null)
        {
            var digits = NormalizeDigits(cpfCnpjDigits);
            if (string.IsNullOrWhiteSpace(digits))
            {
                return false;
            }

            await using var db = new AppDbContext();
            var query = db.Clientes.AsNoTracking().AsQueryable();
            if (excludeId.HasValue)
            {
                query = query.Where(x => x.Id != excludeId.Value);
            }

            var docs = await query
                .Select(x => new { x.CPF, x.Documento })
                .ToListAsync();

            return docs.Any(x =>
                NormalizeDigits(x.CPF) == digits ||
                NormalizeDigits(x.Documento) == digits);
        }

        public async Task AddAsync(Cliente cliente)
        {
            if (cliente.Id == Guid.Empty)
            {
                cliente.Id = Guid.NewGuid();
            }

            var now = DateTime.UtcNow;
            cliente.CriadoEm = now;
            cliente.AtualizadoEm = now;

            await using var db = new AppDbContext();
            db.Clientes.Add(cliente);
            await db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Cliente cliente)
        {
            cliente.AtualizadoEm = DateTime.UtcNow;

            await using var db = new AppDbContext();
            db.Clientes.Update(cliente);
            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var db = new AppDbContext();
            var entity = await db.Clientes.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return;
            }

            db.Clientes.Remove(entity);
            await db.SaveChangesAsync();
        }

        private static string? NormalizeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = Regex.Replace(value, "[^0-9]", string.Empty);
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }
    }
}
