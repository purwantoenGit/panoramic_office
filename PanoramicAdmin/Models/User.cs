namespace PanoramicAdmin.Models;

public sealed record User(int Id, string FullName, string Email, string Role, bool IsActive);
