using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.UseCases;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure
{
    public class DocumentoAnexoRepository : IDocumentoAnexoRepository
    {
        public async Task<List<DocumentoAnexo>> ListByPagamentoAsync(Guid pagamentoId)
        {
            await using var db = new AppDbContext();
            return await db.DocumentosAnexo
                .AsNoTracking()
                .Where(x => x.PagamentoId == pagamentoId && x.Contexto == "Pagamento")
                .OrderByDescending(x => x.CriadoEm)
                .ToListAsync();
        }

        public async Task<List<DocumentoAnexo>> ListByClienteAlvaraAsync(Guid clienteId)
        {
            await using var db = new AppDbContext();
            return await db.DocumentosAnexo
                .AsNoTracking()
                .Where(x => x.ClienteId == clienteId && x.Contexto == "Alvara")
                .OrderByDescending(x => x.CriadoEm)
                .ToListAsync();
        }

        public async Task<DocumentoAnexo?> GetByIdAsync(Guid id)
        {
            await using var db = new AppDbContext();
            return await db.DocumentosAnexo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task AddAsync(DocumentoAnexo documentoAnexo)
        {
            if (documentoAnexo.Id == Guid.Empty)
            {
                documentoAnexo.Id = Guid.NewGuid();
            }

            var now = DateTime.UtcNow;
            if (documentoAnexo.CriadoEm == default)
            {
                documentoAnexo.CriadoEm = now;
            }
            documentoAnexo.AtualizadoEm = now;

            await using var db = new AppDbContext();
            db.DocumentosAnexo.Add(documentoAnexo);
            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var db = new AppDbContext();
            var entity = await db.DocumentosAnexo.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                return;
            }

            db.DocumentosAnexo.Remove(entity);
            await db.SaveChangesAsync();
        }
    }
}
