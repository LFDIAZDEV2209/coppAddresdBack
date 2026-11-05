using CoppAddresd.Domain.ValueObjects;
using System.Text.Json;
using FluentValidation;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Contacto de emergencia estructurado del paciente. Se persiste serializado
/// como JSON (<c>{name,relationship,phone,email}</c>, claves en minúscula) en
/// la misma columna <c>emergency_contact</c>; el formato coincide con el que
/// escribe/lee la app móvil en el perfil self-service.
/// </summary>
public sealed record EmergencyContactDto(
    string? Name,
    string? Relationship,
    string? Phone,
    string? Email,
    SecondaryEmergencyContactDto? Secondary = null);

/// <summary>Segundo contacto opcional; el contacto principal conserva el contrato SOS.</summary>
public sealed record SecondaryEmergencyContactDto(string? Name, string? Relationship, string? Phone, string? Email);

public sealed class EmergencyContactValidator : AbstractValidator<EmergencyContactDto>
{
    public EmergencyContactValidator()
    {
        RuleFor(x => x.Secondary).Custom((secondary, context) =>
        {
            if (secondary is null || new[] { secondary.Name, secondary.Relationship, secondary.Phone, secondary.Email }.All(string.IsNullOrWhiteSpace)) return;
            if (string.IsNullOrWhiteSpace(secondary.Name) || string.IsNullOrWhiteSpace(secondary.Phone)) context.AddFailure("El segundo contacto requiere nombre y teléfono.");
            if (secondary.Name?.Length > 200 || secondary.Relationship?.Length > 100 || secondary.Phone?.Length > 20 || secondary.Email?.Length > 320) context.AddFailure("Revisa los datos del segundo contacto.");
            if (!string.IsNullOrWhiteSpace(secondary.Phone) && EmergencyContactPhone.Normalize(secondary.Phone) is null) context.AddFailure("El teléfono del segundo contacto no es válido.");
            if (!string.IsNullOrWhiteSpace(secondary.Email) && !System.Net.Mail.MailAddress.TryCreate(secondary.Email.Trim(), out _)) context.AddFailure("El correo del segundo contacto no tiene un formato válido.");
        });

        RuleFor(x => x.Name)
            .MaximumLength(200);

        RuleFor(x => x.Relationship)
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("El contacto de emergencia requiere teléfono.");

        RuleFor(x => x.Phone).Must(phone => string.IsNullOrWhiteSpace(phone) || EmergencyContactPhone.Normalize(phone) is not null)
            .WithMessage("Ingresa un teléfono válido con código de país.");

        RuleFor(x => x.Email)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo del contacto de emergencia no tiene un formato válido.");
    }
}

/// <summary>
/// Serializa/deserializa el contacto de emergencia hacia la columna
/// <c>emergency_contact</c>. Reglas: sin nombre ni teléfono → null; texto
/// legacy no-JSON se trata como nombre (compatibilidad con el dato importado).
/// </summary>
public static class EmergencyContactCodec
{
    public static string? Serialize(EmergencyContactDto? contact)
    {
        if (contact is null)
            return null;

        var name = PatientOptions.Normalize(contact.Name);
        var phone = PatientOptions.Normalize(contact.Phone);
        if (name is null && phone is null)
            return null;

        var payload = new
        {
            name,
            relationship = PatientOptions.Normalize(contact.Relationship),
            phone,
            email = PatientOptions.Normalize(contact.Email),
            secondary = NormalizeSecondary(contact.Secondary),
        };

        return JsonSerializer.Serialize(payload);
    }

    public static EmergencyContactDto? Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('{'))
            return new EmergencyContactDto(trimmed, null, null, null);

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new EmergencyContactDto(trimmed, null, null, null);

            var root = document.RootElement;
            return new EmergencyContactDto(
                ReadString(root, "name"),
                ReadString(root, "relationship"),
                ReadString(root, "phone"),
                ReadString(root, "email"),
                root.TryGetProperty("secondary", out var secondary) && secondary.ValueKind == JsonValueKind.Object
                    ? new SecondaryEmergencyContactDto(ReadString(secondary, "name"), ReadString(secondary, "relationship"), ReadString(secondary, "phone"), ReadString(secondary, "email"))
                    : null);
        }
        catch (JsonException)
        {
            return new EmergencyContactDto(trimmed, null, null, null);
        }
    }

    private static object? NormalizeSecondary(SecondaryEmergencyContactDto? contact)
    {
        if (contact is null || (string.IsNullOrWhiteSpace(contact.Name) && string.IsNullOrWhiteSpace(contact.Phone))) return null;
        return new { name = PatientOptions.Normalize(contact.Name), relationship = PatientOptions.Normalize(contact.Relationship), phone = PatientOptions.Normalize(contact.Phone), email = PatientOptions.Normalize(contact.Email) };
    }

    private static string? ReadString(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
