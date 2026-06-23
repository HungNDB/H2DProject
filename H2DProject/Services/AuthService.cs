using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using H2DProject.Data;

namespace H2DProject.Services;

public class AuthService
{
    private readonly H2DDbContext _db;

    public AuthService(H2DDbContext db) => _db = db;

    public async Task<Staff?> ValidateAsync(string username, string password)
    {
        var hash = HashPassword(password);
        return await _db.Staff
            .FirstOrDefaultAsync(s => s.Username == username
                                   && s.PasswordHash == hash
                                   && s.IsActive == true);
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}
