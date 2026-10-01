using System.Text.RegularExpressions;

namespace IoTMonitor.Api.Security;

public static partial class UserCredentials
{
    public static string NormalizeUsername(string username)
    {
        return username.Trim().ToUpperInvariant();
    }

    public static bool IsValidUsername(string? username)
    {
        return username is not null && UsernamePattern().IsMatch(username);
    }

    public static bool IsValidPassword(string? password)
    {
        return password is not null &&
            password.Length is >= 12 and <= 128 &&
            password.Any(char.IsUpper) &&
            password.Any(char.IsLower) &&
            password.Any(char.IsDigit) &&
            password.Any(character => !char.IsLetterOrDigit(character));
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
