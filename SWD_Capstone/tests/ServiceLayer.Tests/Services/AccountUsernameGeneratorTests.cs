using CPMS.Core.Exceptions;
using CPMS.Core.Services;

namespace ServiceLayer.Tests.Services;

public sealed class AccountUsernameGeneratorTests
{
    [Theory]
    [InlineData("Dương Thành Thanh Duy", "SE194673", "DuyDTTSE194673")]
    [InlineData("Đặng Quốc Đạt", "SE000001", "DatDQSE000001")]
    [InlineData("  Nguyễn   Văn   An  ", "se123456", "AnNVSE123456")]
    [InlineData("Lê Minh", " lec-009 ", "MinhLLEC009")]
    public void Generate_BuildsUsernameFromGivenNameInitialsAndCode(
        string fullName,
        string identityCode,
        string expected)
    {
        var actual = AccountUsernameGenerator.Generate(fullName, identityCode);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_RejectsMissingFullName(string fullName)
    {
        var action = () => AccountUsernameGenerator.Generate(fullName, "SE194673");

        Assert.Throws<BusinessRuleException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_RejectsMissingIdentityCode(string identityCode)
    {
        var action = () => AccountUsernameGenerator.Generate("Dương Thành Thanh Duy", identityCode);

        Assert.Throws<BusinessRuleException>(action);
    }
}
