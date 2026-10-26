using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
}
