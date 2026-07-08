namespace CPMS.Application.Abstractions.Identity;

public interface IAccountUsernameGenerator
{
    string Generate(string fullName, string identityCode);
}
