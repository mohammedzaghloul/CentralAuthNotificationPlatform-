using System.ComponentModel.DataAnnotations;

namespace CentralAuthNotificationPlatform.Dtos;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(2), MaxLength(120)] string DisplayName,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [MaxLength(24)] string Role = "User");

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed class ForgotPasswordRequest : IValidatableObject
{
    [MaxLength(256)]
    public string? ExternalUserId { get; init; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [Url, MaxLength(500)]
    public string? ResetUrlBase { get; init; }

    public string UserIdentifier =>
        string.IsNullOrWhiteSpace(ExternalUserId)
            ? Email?.Trim() ?? string.Empty
            : ExternalUserId.Trim();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ExternalUserId) && string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult(
                "externalUserId is required.",
                new[] { nameof(ExternalUserId) });
        }
    }
}

public sealed record ForgotPasswordResponse(
    bool Accepted,
    string Message,
    DateTimeOffset? ExpiresAt);

public sealed record ValidateResetTokenRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(16), MaxLength(512)] string Token);

public sealed record ValidateResetTokenResponse(bool IsValid, DateTimeOffset? ExpiresAt);

public sealed record ResetPasswordRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(16), MaxLength(512)] string Token,
    [Required, MinLength(8), MaxLength(128)] string NewPassword);

public sealed record MessageResponse(string Message);
