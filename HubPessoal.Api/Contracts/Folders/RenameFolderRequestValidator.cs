using FluentValidation;

namespace HubPessoal.Api.Contracts.Folders;

public class RenameFolderRequestValidator : AbstractValidator<RenameFolderRequest>
{
    public RenameFolderRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}