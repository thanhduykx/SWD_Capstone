using CPMS.Application.Abstractions.Security;
using CPMS.Core.Entities;

namespace CPMS.Infrastructure.ExternalServices;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(User user, string password) =>
        BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
}
