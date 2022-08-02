using System.Threading.Tasks;

namespace Refit.Tests
{
    public interface IFooWithOtherAttribute
    {
        [Get("/")]
        Task GetRoot();

        [Post("/")]
        Task PostRoot();
    }
}
