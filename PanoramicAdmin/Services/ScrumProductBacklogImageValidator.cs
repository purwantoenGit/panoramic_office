using Microsoft.AspNetCore.Components.Forms;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public static class ScrumProductBacklogImageValidator
{
    public const int MaximumImageSize = 5 * 1024 * 1024;
    public const int MaximumImagesPerItem = 10;

    public static async Task<IReadOnlyList<ScrumProductBacklogImageUpload>> ReadAsync(
        IReadOnlyList<IBrowserFile> files)
    {
        if (files.Count > MaximumImagesPerItem)
        {
            throw new InvalidDataException($"Pilih maksimal {MaximumImagesPerItem} gambar.");
        }

        var images = new List<ScrumProductBacklogImageUpload>(files.Count);
        foreach (var file in files)
        {
            if (file.Size is <= 0 or > MaximumImageSize)
            {
                throw new InvalidDataException("Ukuran setiap gambar harus lebih dari 0 dan maksimal 5 MB.");
            }

            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
            await using var source = file.OpenReadStream(MaximumImageSize);
            await using var buffer = new MemoryStream((int)file.Size);
            await source.CopyToAsync(buffer);
            var image = new ScrumProductBacklogImageUpload(buffer.ToArray(), file.ContentType, extension);
            Validate(image);
            images.Add(image);
        }

        return images;
    }

    public static void Validate(ScrumProductBacklogImageUpload image)
    {
        var expectedContentType = image.Extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
        if (expectedContentType is null ||
            !StringComparer.OrdinalIgnoreCase.Equals(image.ContentType, expectedContentType))
        {
            throw new InvalidDataException("Format gambar harus JPG, PNG, atau WebP.");
        }

        var content = image.Content;
        if (content.Length is <= 0 or > MaximumImageSize)
        {
            throw new InvalidDataException("Ukuran setiap gambar harus lebih dari 0 dan maksimal 5 MB.");
        }

        var valid = expectedContentType switch
        {
            "image/jpeg" => content.Length >= 3 &&
                content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/png" => content.Length >= 8 &&
                content.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => content.Length >= 12 &&
                content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid)
        {
            throw new InvalidDataException("Isi file tidak sesuai dengan format gambar yang dipilih.");
        }
    }
}
