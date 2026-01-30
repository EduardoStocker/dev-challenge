using System.Threading.Tasks;
using Desafio.Umbler.Models.DTOs;

namespace Desafio.Umbler.Services.Interfaces
{
    public interface IDomainService
    {
        Task<DomainDto> GetDomainInfoAsync(string domainName);
    }
}