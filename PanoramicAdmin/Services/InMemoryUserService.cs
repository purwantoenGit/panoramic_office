using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class InMemoryUserService : IUserService
{
    private static readonly IReadOnlyList<User> Users =
    [
        new(1, "Andi Pratama", "andi.pratama@example.com", "Administrator", true),
        new(2, "Siti Rahayu", "siti.rahayu@example.com", "Manajer", true),
        new(3, "Budi Santoso", "budi.santoso@example.com", "Staf", true),
        new(4, "Dewi Lestari", "dewi.lestari@example.com", "Staf", false),
        new(5, "Rizky Hidayat", "rizky.hidayat@example.com", "Staf", true)
    ];

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Users);
}
