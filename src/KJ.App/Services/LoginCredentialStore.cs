using System.Security.Cryptography;
using System.Text;
using Windows.Storage;

namespace KJ.App.Services;

public sealed class LoginCredentialStore : KJ.Modules.Auth.ILoginCredentialStore
{
    private const string RememberedEmailKey = "KJ_RememberedEmail";
    private const string StaySignedInFileName = "kj-stay-signed-in.dat";

    public string? LoadRememberedEmail()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(RememberedEmailKey, out var v) &&
                v is string s &&
                !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            return null;
        }
        catch
        {
            // Unpackaged/early-startup scenarios may not have ApplicationData available.
            return null;
        }
    }

    public void SaveRememberedEmail(string email)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[RememberedEmailKey] = email;
        }
        catch
        {
        }
    }

    public void ClearRememberedEmail()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey(RememberedEmailKey))
                ApplicationData.Current.LocalSettings.Values.Remove(RememberedEmailKey);
        }
        catch
        {
        }
    }

    public void SaveStaySignedIn(string email, string password)
    {
        try
        {
            var folder = ApplicationData.Current.LocalFolder.Path;
            var path = Path.Combine(folder, StaySignedInFileName);
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
            var folder = ApplicationData.Current.LocalFolder.Path;
            var path = Path.Combine(folder, StaySignedInFileName);
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
            var folder = ApplicationData.Current.LocalFolder.Path;
            var path = Path.Combine(folder, StaySignedInFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 忽略删除失败
        }
    }
}
