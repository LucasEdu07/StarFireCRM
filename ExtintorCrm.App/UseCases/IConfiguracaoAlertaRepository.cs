using System.Threading.Tasks;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.UseCases
{
    public interface IConfiguracaoAlertaRepository
    {
        Task<ConfiguracaoAlerta> GetAsync();
        Task SaveAsync(ConfiguracaoAlerta configuracao);
    }
}
