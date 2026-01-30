using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Desafio.Umbler.Service.Interfaces;
using Whois.NET;

namespace Desafio.Umbler.Service
{
    public class WhoisClientWrapper : IWhoisClient
    {
        public async Task<WhoisResponse> QueryAsync(string domain)
        {
            return await WhoisClient.QueryAsync(
                domain,
                null,
                43,
                Encoding.ASCII,
                5000,
                1,
                CancellationToken.None
            );
        }
    }
}
