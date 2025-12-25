using FluentValidation;
using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Validators.Authentication;

/// <summary>
/// Validator for refresh token requests
/// </summary>
public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required");
    }
}
