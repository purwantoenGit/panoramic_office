namespace PanoramicAdmin.Models;

public class MasterPic
{
    public int Id { get; set; }
    public required string Nama { get; set; }
    public required string Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? WhatsAppId { get; set; }
    public string? Email { get; set; }
}

public sealed record MasterPicInput(
    string Nama,
    string Role,
    string? PhoneNumber = null,
    string? WhatsAppId = null,
    string? Email = null);

public static class MasterPicContact
{
    public static bool TryNormalizeEmail(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim();
        if (candidate.Length > 254 ||
            !System.Net.Mail.MailAddress.TryCreate(candidate, out var address) ||
            !string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static bool TryNormalizePhoneNumber(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        var containsInvalidCharacter = trimmed.Any(character =>
            (!char.IsAsciiDigit(character) &&
             character is not ('+' or ' ' or '-' or '(' or ')')) ||
            (character == '+' && trimmed.IndexOf('+') != 0));
        if (containsInvalidCharacter || trimmed.Count(character => character == '+') > 1)
        {
            return false;
        }

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 8 or > 15 || digits[0] == '0')
        {
            return false;
        }

        normalized = $"+{digits}";
        return true;
    }

    public static bool TryNormalizeWhatsAppId(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim().ToLowerInvariant();
        var separatorIndex = candidate.IndexOf('@');
        if (separatorIndex < 1 ||
            candidate.IndexOf('@', separatorIndex + 1) >= 0 ||
            !candidate[..separatorIndex].All(char.IsAsciiDigit))
        {
            return false;
        }

        var server = candidate[(separatorIndex + 1)..];
        if (server is not ("c.us" or "s.whatsapp.net" or "lid"))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
