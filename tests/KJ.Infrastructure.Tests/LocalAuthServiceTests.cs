using FluentAssertions;
using KJ.Infrastructure.Auth;
using KJ.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KJ.Infrastructure.Tests;

public class LocalAuthServiceTests : IDisposable
{
    private readonly KjDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly LocalAuthService _sut;

    public LocalAuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<KjDbContext>()
            .UseInMemoryDatabase($"AuthTest_{Guid.NewGuid()}")
            .Options;

        _db = new KjDbContext(options);

        var store = new UserStore<IdentityUser>(_db);
        var userValidator = new UserValidator<IdentityUser>();
        var passwordValidators = new List<PasswordValidator<IdentityUser>> { new() };
        var lookupNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();

        _userManager = new UserManager<IdentityUser>(
            store, null, new PasswordHasher<IdentityUser>(),
            new[] { userValidator }, passwordValidators, lookupNormalizer, errors, null,
            NullLogger<UserManager<IdentityUser>>.Instance);

        _sut = new LocalAuthService(_userManager);
    }

    public void Dispose()
    {
        _userManager.Dispose();
        _db.Dispose();
    }

    private async Task<IdentityUser> CreateUserAsync(string email, string password)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue("Failed to create test user");
        return user;
    }

    [Fact]
    public async Task SignInAsync_ShouldFail_WhenEmailEmpty()
    {
        var (success, error) = await _sut.SignInAsync("", "password123");

        success.Should().BeFalse();
        error.Should().Contain("邮箱");
    }

    [Fact]
    public async Task SignInAsync_ShouldFail_WhenPasswordEmpty()
    {
        var (success, error) = await _sut.SignInAsync("test@example.com", "");

        success.Should().BeFalse();
        error.Should().Contain("密码");
    }

    [Fact]
    public async Task SignInAsync_ShouldFail_WhenUserNotFound()
    {
        var (success, error) = await _sut.SignInAsync("nonexistent@example.com", "password123");

        success.Should().BeFalse();
        error.Should().Contain("不存在");
    }

    [Fact]
    public async Task SignInAsync_ShouldFail_WhenWrongPassword()
    {
        await CreateUserAsync("test@example.com", "CorrectPass123!");

        var (success, error) = await _sut.SignInAsync("test@example.com", "WrongPass");

        success.Should().BeFalse();
        error.Should().Contain("密码");
    }

    [Fact]
    public async Task SignInAsync_ShouldSucceed_WhenCredentialsValid()
    {
        await CreateUserAsync("test@example.com", "ValidPass123!");

        var (success, error) = await _sut.SignInAsync("test@example.com", "ValidPass123!");

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task SignInAsync_ShouldHandleNullEmail()
    {
        var (success, error) = await _sut.SignInAsync(null!, "password");

        success.Should().BeFalse();
    }

    [Fact]
    public async Task SignInAsync_ShouldHandleNullPassword()
    {
        var (success, error) = await _sut.SignInAsync("test@example.com", null!);

        success.Should().BeFalse();
    }
}
