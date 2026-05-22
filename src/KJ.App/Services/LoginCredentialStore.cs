using System.Security.Cryptography;
using System.Text;

namespace KJ.App.Services;

public sealed class LoginCredentialStore : KJ.Modules.Auth.ILoginCredentialStore
{
    private const string RememberedEmailFileName = "kj-remembered-email.txt";
    private const string StaySignedInFileName = "kj-stay-signed-in.dat";

    private static string GetAppDataFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KJ");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string? LoadRememberedEmail()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), RememberedEmailFileName);
            if (!File.Exists(path))
                return null;

            var email = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }
        catch
        {
            return null;
        }
    }

    public void SaveRememberedEmail(string email)
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), RememberedEmailFileName);
            File.WriteAllText(path, email);
        }
        catch
        {
        }
    }

    public void ClearRememberedEmail()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), RememberedEmailFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    public void SaveStaySignedIn(string email, string password)
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), StaySignedInFileName);
            var plain = Encoding.UTF8.GetBytes($"{email}\n{password}");
            var blob = ProtectedData.Protect(plain, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, blob);
        }
        catch
        {
        }
    }

    public (string? Email, string? Password) TryLoadStaySignedIn()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), StaySignedInFileName);
            if (!File.Exists(path))
                return (null, null);

            var blob = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(blob, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            var text = Encoding.UTF8.GetString(plain);
            var idx = text.IndexOf('\n');
            if (idx <= 0 || idx >= text.Length - 1)
                return (null, null);

            var email = text[..idx].Trim();
            var password = text[(idx + 1)..];
            return string.IsNullOrWhiteSpace(email) ? (null, null) : (email, password);
        }
        catch
        {
            ClearStaySignedIn();
            return (null, null);
        }
    }

    public void ClearStaySignedIn()
    {
        try
        {
            var path = Path.Combine(GetAppDataFolder(), StaySignedInFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
