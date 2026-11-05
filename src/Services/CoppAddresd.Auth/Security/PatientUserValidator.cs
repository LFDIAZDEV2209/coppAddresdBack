using System.Text.RegularExpressions;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;

namespace CoppAddresd.Auth.Security;

/// <summary>
/// Conserva las reglas de Identity. Solo permite correo ausente para el alta
/// de un paciente identificado por GUID cuyo celular ya verificó el servidor.
/// </summary>
public sealed class PatientUserValidator(IdentityErrorDescriber errors)
    : UserValidator<ApplicationUser>(errors)
{
    public override async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var result = await base.ValidateAsync(manager, user);
        var verifiedPhonePatient = string.IsNullOrWhiteSpace(user.Email)
            && user.PhoneNumberConfirmed
            && Regex.IsMatch(user.PhoneNumber ?? "", @"^\+[1-9][0-9]{7,14}$")
            && Regex.IsMatch(user.UserName ?? "", @"^patient-[a-f0-9]{32}$");
        if (result.Succeeded || !verifiedPhonePatient) return result;

        // No se descartan duplicados de usuario ni errores de correos presentes.
        var remaining = result.Errors.Where(error => error.Code != nameof(IdentityErrorDescriber.InvalidEmail)).ToArray();
        return remaining.Length == 0 ? IdentityResult.Success : IdentityResult.Failed(remaining);
    }
}
