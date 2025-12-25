using FluentValidation;
using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Validators.Authentication;

/// <summary>
/// Validator for login requests
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(255).WithMessage("Username must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");

        RuleFor(x => x.MfaCode)
            .Length(6).WithMessage("MFA code must be exactly 6 digits")
            .When(x => !string.IsNullOrEmpty(x.MfaCode));
    }
}
