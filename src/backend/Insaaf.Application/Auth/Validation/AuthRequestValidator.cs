using System.ComponentModel.DataAnnotations;
using Insaaf.Application.Auth.Dtos;

namespace Insaaf.Application.Auth.Validation;

public static class AuthRequestValidator
{
    public static IReadOnlyList<string> ValidateRegister(RegisterRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("Email is required.");
        }
        else if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            errors.Add("Email format is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors.Add("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors.Add("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }
        else if (request.Password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateLogin(LoginRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateRefresh(RefreshRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            errors.Add("Refresh token is required.");
        }

        return errors;
    }
}
