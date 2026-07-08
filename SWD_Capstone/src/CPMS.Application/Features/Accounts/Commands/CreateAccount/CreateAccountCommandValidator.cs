using FluentValidation;

namespace CPMS.Application.Features.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(128);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentityCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.Position).MaximumLength(100);
        RuleFor(x => x.PermissionScope).MaximumLength(100);
        RuleFor(x => x.ClassCode).MaximumLength(50);
        RuleFor(x => x.Batch).MaximumLength(50);
        RuleFor(x => x.Major).MaximumLength(100);
    }
}
