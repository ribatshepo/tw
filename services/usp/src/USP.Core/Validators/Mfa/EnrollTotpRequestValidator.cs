using FluentValidation;
using USP.Core.Models.DTOs.Mfa;

namespace USP.Core.Validators.Mfa;

/// <summary>
/// Validator for TOTP enrollment requests
/// </summary>
public class EnrollTotpRequestValidator : AbstractValidator<EnrollTotpRequest>
{
    public EnrollTotpRequestValidator()
    {
        RuleFor(x => x.DeviceName)
            .NotEmpty().WithMessage("Device name is required")
            .MinimumLength(3).WithMessage("Device name must be at least 3 characters")
            .MaximumLength(100).WithMessage("Device name must not exceed 100 characters");
    }
}

/// <summary>
/// Validator for TOTP verification requests
/// </summary>
public class VerifyTotpRequestValidator : AbstractValidator<VerifyTotpRequest>
{
    public VerifyTotpRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required")
            .Length(6).WithMessage("Verification code must be 6 digits")
            .Matches("^[0-9]{6}$").WithMessage("Verification code must contain only digits");
    }
}

/// <summary>
/// Validator for disable MFA requests
/// </summary>
public class DisableMfaRequestValidator : AbstractValidator<DisableMfaRequest>
{
    public DisableMfaRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required for disabling MFA");
    }
}

/// <summary>
/// Validator for OTP enrollment requests
/// </summary>
public class EnrollOtpRequestValidator : AbstractValidator<EnrollOtpRequest>
{
    public EnrollOtpRequestValidator()
    {
        RuleFor(x => x.DeviceType)
            .NotEmpty().WithMessage("Device type is required")
            .Must(x => x == "SMS" || x == "Email").WithMessage("Device type must be SMS or Email");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required for SMS")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format")
            .When(x => x.DeviceType == "SMS");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required for Email OTP")
            .EmailAddress().WithMessage("Invalid email format")
            .When(x => x.DeviceType == "Email");
    }
}
