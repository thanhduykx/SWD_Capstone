using CPMS.BLL.Abstractions.Security;

namespace CPMS.DAL.ExternalServices;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string passwordHash, string password)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, passwordHash); }
        catch (ArgumentException) { return false; }
    }
}
