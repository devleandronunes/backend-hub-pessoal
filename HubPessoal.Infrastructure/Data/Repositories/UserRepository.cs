using HubPessoal.Application.Interfaces;
using HubPessoal.Domain.Entities;
using HubPessoal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HubPessoal.Infrastructure.Data.Repositories;

public class userRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public userRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByUsernameAsync(string username)
    {
        return _context.Users.SingleOrDefaultAsync(u => u.Username == username);
    }
}