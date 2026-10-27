namespace PanoramicAdmin.Models;

public sealed record ProjectInput(
    string Name,
    string Stream,
    string CategoryProject,
    string LogoIcon,
    string[] Sites);
