using FluentValidation;
using USP.API.DTOs.Authentication;

namespace USP.API.Validators.Authentication;

/// <summary>
/// Validator for password reset requests.
/// </summary>
public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required")
            .MinimumLength(20).WithMessage("Invalid reset token format");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(12).WithMessage("New password must be at least 12 characters")
            .Matches(@"[A-Z]").WithMessage("New password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("New password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("New password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("New password must contain at least one special character");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("New password confirmation is required")
            .Equal(x => x.NewPassword).WithMessage("New passwords do not match");
    }
}
