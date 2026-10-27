namespace Ardak.Application.DTOs;

public class SignInResultDto
{
    public bool Succeeded { get; set; }
    public bool IsLockedOut { get; set; }
    public bool IsNotAllowed { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? ErrorMessage { get; set; }

    public static SignInResultDto Success() => new() { Succeeded = true };
    public static SignInResultDto Failed(string error) => new() { Succeeded = false, ErrorMessage = error };
    public static SignInResultDto LockedOut() => new() { Succeeded = false, IsLockedOut = true, ErrorMessage = "Account is locked out." };
    public static SignInResultDto NotAllowed() => new() { Succeeded = false, IsNotAllowed = true, ErrorMessage = "Account is not active or not allowed to sign in." };
}
