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
    string? Email);

public sealed class EmergencyContactValidator : AbstractValidator<EmergencyContactDto>
{
    public EmergencyContactValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200);

        RuleFor(x => x.Relationship)
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("El contacto de emergencia requiere teléfono.");

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
                ReadString(root, "email"));
        }
        catch (JsonException)
        {
            return new EmergencyContactDto(trimmed, null, null, null);
        }
    }

    private static string? ReadString(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
