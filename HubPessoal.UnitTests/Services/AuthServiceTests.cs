using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Services;
using HubPessoal.Domain.Entities;
using Moq;

namespace HubPessoal.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository.Object, _passwordHasher.Object, _tokenService.Object);
    }

    [Fact]
    public async Task LoginAsync_WithNonexistentUser_ReturnsNull()
    {
        _userRepository.Setup(r => r.GetByUsernameAsync("ninguem")).ReturnsAsync((User?)null);

        var token = await _sut.LoginAsync("ninguem", "qualquer");

        Assert.Null(token);
        _tokenService.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        var user = new User("dev-user", "hash-correto");
        _userRepository.Setup(r => r.GetByUsernameAsync("dev-user")).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("senha-errada", "hash-correto")).Returns(false);

        var token = await _sut.LoginAsync("dev-user", "senha-errada");

        Assert.Null(token);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokenGeneratedForThatUser()
    {
        var user = new User("dev-user", "hash-correto");
        _userRepository.Setup(r => r.GetByUsernameAsync("dev-user")).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("senha-certa", "hash-correto")).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(user)).Returns("token-fake-para-dev-user");

        var token = await _sut.LoginAsync("dev-user", "senha-certa");

        Assert.Equal("token-fake-para-dev-user", token);
        _tokenService.Verify(t => t.GenerateToken(It.Is<User>(u => u.Username == "dev-user")), Times.Once);
    }
}
