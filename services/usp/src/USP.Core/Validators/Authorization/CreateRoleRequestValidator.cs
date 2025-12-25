using FluentValidation;
using USP.Core.Models.DTOs.Authorization;

namespace USP.Core.Validators.Authorization;

/// <summary>
/// Validator for create role requests
/// </summary>
public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required")
            .MinimumLength(3).WithMessage("Role name must be at least 3 characters")
            .MaximumLength(100).WithMessage("Role name must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Role name can only contain letters, numbers, underscores, and hyphens");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
