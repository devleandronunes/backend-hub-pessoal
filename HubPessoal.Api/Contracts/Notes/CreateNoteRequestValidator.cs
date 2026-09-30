using FluentValidation;

namespace HubPessoal.Api.Contracts.Notes;

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ResolveContent())
            .NotNull()
            .OverridePropertyName("Content")
            .WithMessage("Content must be sent as plain text or valid Base64.");
    }
}