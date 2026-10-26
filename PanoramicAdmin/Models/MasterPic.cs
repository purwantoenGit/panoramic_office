namespace PanoramicAdmin.Models;

public class MasterPic
{
    public int Id { get; set; }
    public required string Nama { get; set; }
    public required string Role { get; set; }
}

public sealed record MasterPicInput(string Nama, string Role);
