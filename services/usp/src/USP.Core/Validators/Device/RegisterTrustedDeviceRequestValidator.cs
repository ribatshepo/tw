using FluentValidation;
using USP.Core.Models.DTOs.Device;

namespace USP.Core.Validators.Device;

public class RegisterTrustedDeviceRequestValidator : AbstractValidator<RegisterTrustedDeviceRequest>
{
    public RegisterTrustedDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceName)
            .NotEmpty().WithMessage("Device name is required")
            .MinimumLength(3).WithMessage("Device name must be at least 3 characters")
            .MaximumLength(100).WithMessage("Device name must not exceed 100 characters");
    }
}
