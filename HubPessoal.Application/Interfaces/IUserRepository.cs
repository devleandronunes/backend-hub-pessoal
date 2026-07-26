using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
}
