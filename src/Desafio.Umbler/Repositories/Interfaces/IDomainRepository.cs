using System.Threading.Tasks;
using Desafio.Umbler.Models;

namespace Desafio.Umbler.Repositories.Interfaces
{
    public interface IDomainRepository
    {
        Task<Domain> GetByNameAsync(string domainName);
        Task<Domain> AddAsync(Domain domain);
        Task<Domain> UpdateAsync(Domain domain);
        Task SaveChangesAsync();
    }
}