namespace CPMS.BLL.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}
