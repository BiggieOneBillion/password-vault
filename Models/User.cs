using System;

namespace PasswordVault.Models;

public class User
{

}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterationRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class AddMorePasswordRequest
{
    public string Password { get; set; } = string.Empty;
      
    public string Field { get; set; } = string.Empty;
}
