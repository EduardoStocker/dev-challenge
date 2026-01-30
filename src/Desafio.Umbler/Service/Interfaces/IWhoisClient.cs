using System.Threading.Tasks;
using Whois.NET;

namespace Desafio.Umbler.Service.Interfaces
{
    public interface IWhoisClient
    {
        Task<WhoisResponse> QueryAsync(string domain);
    }

}
