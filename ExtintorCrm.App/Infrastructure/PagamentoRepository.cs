using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Documents;
using ExtintorCrm.App.UseCases;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure
{
    public class PagamentoRepository : IPagamentoRepository
    {
        public async Task<List<Pagamento>> GetAllAsync()
        {
            await using var db = new AppDbContext();
            return await db.Pagamentos.AsNoTracking().ToListAsync();
        }

        public async Task<Pagamento?> GetByIdAsync(Guid id)
        {
            await using var db = new AppDbContext();
            return await db.Pagamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task AddAsync(Pagamento pagamento)
        {
            if (pagamento.Id == Guid.Empty)
            {
                pagamento.Id = Guid.NewGuid();
            }

            var now = DateTime.UtcNow;
            pagamento.CriadoEm = now;
            pagamento.AtualizadoEm = now;

            await using var db = new AppDbContext();
            db.Pagamentos.Add(pagamento);
            await db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Pagamento pagamento)
        {
            pagamento.AtualizadoEm = DateTime.UtcNow;
            await using var db = new AppDbContext();
            db.Pagamentos.Update(pagamento);
            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var db = new AppDbContext();
            var entity = await db.Pagamentos.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return;
            }

            var anexos = await db.DocumentosAnexo
                .Where(x => x.PagamentoId == id)
                .ToListAsync();
            if (anexos.Count > 0)
            {
                var storage = new DocumentoStorageService();
                foreach (var anexo in anexos)
                {
                    storage.DeleteByRelativePath(anexo.CaminhoRelativo);
                }

                db.DocumentosAnexo.RemoveRange(anexos);
            }

            db.Pagamentos.Remove(entity);
            await db.SaveChangesAsync();
        }
    }
}
