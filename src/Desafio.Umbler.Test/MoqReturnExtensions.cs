using System.Threading.Tasks;
using Desafio.Umbler.Service.Interfaces;
using Moq;
using Moq.Language.Flow;

namespace Desafio.Umbler.Test
{
    public static class MoqReturnExtensions
    {
        public static void Return<TResult>(
            this ISetup<IWhoisClient, Task<TResult>> setup)
        {
            setup.ReturnsAsync(default(TResult));
        }
    }
}
