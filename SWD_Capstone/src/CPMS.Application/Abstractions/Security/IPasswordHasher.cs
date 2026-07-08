using CPMS.Core.Entities;

namespace CPMS.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(User user, string password);
}
