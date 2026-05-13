using System.Net.Mail;
using AdminHiitop.Api.Application.DTOs.Auth;
using AdminHiitop.Api.Application.DTOs.Roles;
using AdminHiitop.Api.Application.DTOs.Users;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Helpers;

public static class UserValidationHelper
{
    public static void ValidateLoginRequest(LoginRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud de inicio de sesi\u00f3n es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AppException("El correo y la contrase\u00f1a son obligatorios.");
        }
    }

    public static void ValidateRegisterRequest(RegisterRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud de registro es obligatoria.");
        }

        ValidateName(request.Name);
        ValidateEmail(request.Email);
        ValidatePassword(request.Password, true);

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new AppException("La confirmaci\u00f3n de contrase\u00f1a no coincide.");
        }

        ValidateIds(request.RoleIds, "roles");
    }

    public static void ValidateUserUpsertRequest(UserUpsertRequest request, bool requirePassword)
    {
        if (request is null)
        {
            throw new AppException("La solicitud de usuario es obligatoria.");
        }

        ValidateName(request.Name);
        ValidateEmail(request.Email);
        ValidatePassword(request.Password, requirePassword);
        ValidateIds(request.RoleIds, "roles");
    }

    public static void ValidateRoleUpsertRequest(RoleUpsertRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud de rol es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("El nombre del rol es obligatorio.");
        }

        ValidateIds(request.PermissionIds, "permisos");
    }

    public static string NormalizeEmail(string email)
    {
        ValidateEmail(email);
        return email.Trim().ToLowerInvariant();
    }

    public static string NormalizeName(string name)
    {
        ValidateName(name);
        return name.Trim();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AppException("El nombre es obligatorio.");
        }
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AppException("El correo es obligatorio.");
        }

        try
        {
            MailAddress _ = new(email.Trim());
        }
        catch (FormatException)
        {
            throw new AppException("El correo no tiene un formato v\u00e1lido.");
        }
    }

    private static void ValidatePassword(string? password, bool required)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            if (required)
            {
                throw new AppException("La contrase\u00f1a es obligatoria.");
            }

            return;
        }

        if (password.Trim().Length < 6)
        {
            throw new AppException("La contrase\u00f1a debe tener al menos 6 caracteres.");
        }
    }

    private static void ValidateIds(IReadOnlyCollection<int> ids, string resourceName)
    {
        if (ids.Count == 0)
        {
            return;
        }

        if (ids.Any(item => item <= 0))
        {
            throw new AppException($"La lista de {resourceName} contiene identificadores inv\u00e1lidos.");
        }
    }
}
