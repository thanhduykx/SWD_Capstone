namespace CPMS.BLL.Abstractions.Identity;

public interface IAccountUsernameGenerator
{
    string Generate(string fullName, string identityCode);
}
