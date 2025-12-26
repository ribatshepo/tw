using FluentValidation;
using USP.API.DTOs.Authentication;

namespace USP.API.Validators.Authentication;

/// <summary>
/// Validator for password change requests.
/// </summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(12).WithMessage("New password must be at least 12 characters")
            .Matches(@"[A-Z]").WithMessage("New password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("New password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("New password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("New password must contain at least one special character")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from current password");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("New password confirmation is required")
            .Equal(x => x.NewPassword).WithMessage("New passwords do not match");
    }
}
