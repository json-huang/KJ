namespace KJ.Infrastructure.Auth;

public interface ILocalAuthService
{
    Task<(bool Success, string? ErrorMessage)> SignInAsync(string email, string password, CancellationToken cancellationToken = default);
}
