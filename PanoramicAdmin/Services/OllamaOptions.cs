namespace PanoramicAdmin.Services;

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public string Model { get; set; } = "qwen3:14b";
}
