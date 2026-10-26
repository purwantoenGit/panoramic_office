using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class UserListViewModel(IUserService userService)
{
    private IReadOnlyList<User> _allUsers = [];

    public IReadOnlyList<User> Users { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public bool IsLoading { get; private set; } = true;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            _allUsers = await userService.GetAllAsync(cancellationToken);
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Search(string? text)
    {
        SearchText = text ?? string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Users = string.IsNullOrWhiteSpace(SearchText)
            ? _allUsers
            : _allUsers.Where(user =>
                    user.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    user.Role.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }
}
