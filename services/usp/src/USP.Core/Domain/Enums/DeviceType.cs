namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the type of device used for authentication
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// Desktop or laptop computer
    /// </summary>
    Desktop = 0,

    /// <summary>
    /// Mobile phone
    /// </summary>
    Mobile = 1,

    /// <summary>
    /// Tablet device
    /// </summary>
    Tablet = 2,

    /// <summary>
    /// Unknown or unrecognized device type
    /// </summary>
    Unknown = 3
}
