using System.Threading.Tasks;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.UseCases;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure
{
    public class ConfiguracaoAlertaRepository : IConfiguracaoAlertaRepository
    {
        public async Task<ConfiguracaoAlerta> GetAsync()
        {
            await using var db = new AppDbContext();
            var config = await db.ConfiguracoesAlerta.FirstOrDefaultAsync(x => x.Id == 1);
            return config ?? new ConfiguracaoAlerta();
        }

        public async Task SaveAsync(ConfiguracaoAlerta configuracao)
        {
            await using var db = new AppDbContext();
            var existente = await db.ConfiguracoesAlerta.FirstOrDefaultAsync(x => x.Id == 1);

            if (existente == null)
            {
                configuracao.Id = 1;
                db.ConfiguracoesAlerta.Add(configuracao);
            }
            else
            {
                existente.Alerta7Dias = configuracao.Alerta7Dias;
                existente.Alerta15Dias = configuracao.Alerta15Dias;
                existente.Alerta30Dias = configuracao.Alerta30Dias;
            }

            await db.SaveChangesAsync();
        }
    }
}
