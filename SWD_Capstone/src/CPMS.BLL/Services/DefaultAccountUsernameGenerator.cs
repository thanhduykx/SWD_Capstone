using CPMS.BLL.Abstractions.Identity;

namespace CPMS.BLL.Services;

public sealed class DefaultAccountUsernameGenerator : IAccountUsernameGenerator
{
    public string Generate(string fullName, string identityCode) =>
        AccountUsernameGenerator.Generate(fullName, identityCode);
}
