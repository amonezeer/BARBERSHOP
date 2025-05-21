using ADO_EF.Data;
using ADO_EF.Data.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace ADO_EF.Services
{
    public class DatabaseService
    {
        private readonly DataContext _context;

        public DatabaseService(DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<User?> AuthenticateAsync(string login, string password)
        {
            var userAccess = _context.UserAccesses.FirstOrDefault(ua => ua.Login == login);
            if (userAccess == null) return null;

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password + userAccess.Salt, userAccess.Dk);
            if (!isPasswordValid) return null;

            return await _context.Users.FindAsync(userAccess.UserId);
        }

        public async Task<bool> IsEmailRegisteredAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email && u.DeletedAt == null);
        }

        public async Task<bool> IsUsernameRegisteredAsync(string username)
        {
            return await _context.UserAccesses.AnyAsync(ua => ua.Login == username);
        }

        public async Task RegisterUserAsync(User user, UserAccess userAccess)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var existingUser = await _context.Users.FindAsync(user.Id);
                    if (existingUser != null)
                    {
                        _context.Users.Update(user);
                    }
                    else
                    {
                        await _context.Users.AddAsync(user);
                    }

                    await _context.UserAccesses.AddAsync(userAccess);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}