using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}