namespace PanoramicAdmin.Services;

public sealed class ScrumProductBacklogImageStorage(IWebHostEnvironment environment)
{
    public string RootPath => Path.Combine(environment.ContentRootPath, "App_Data", "PbiImages");

    public string GetDirectory(int productBacklogItemId) =>
        Path.Combine(RootPath, productBacklogItemId.ToString());

    public string GetPath(int productBacklogItemId, string fileName) =>
        Path.Combine(GetDirectory(productBacklogItemId), fileName);

    public string GetLegacyPath(int productBacklogItemId, string fileName) =>
        Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            "uploads", "pbi", productBacklogItemId.ToString(), fileName);
}
