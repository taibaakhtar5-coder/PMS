using System.Threading.Tasks;
using HealthcareCRM.Models;

namespace HealthcareCRM.Services
{
    public interface IAuthService
    {
        Task<User?> RegisterAsync(RegisterViewModel model);
        Task<User?> LoginAsync(LoginViewModel model);
        string GenerateJwtToken(User user);
    }
}
