using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateLeaguePreferences;

/// <summary>
/// Validación de <see cref="UpdateLeaguePreferencesCommand"/> (LEAGUE v1):
/// optIn = true exige nickname (3-32 tras trim, charset acotado a letras
/// (incl. acentuadas), números, espacio, guion y guion bajo); optIn = false
/// deja el nickname opcional (null = limpiar lo almacenado), pero si llega,
/// pasa por el mismo formato. Además se rechazan tokens RESERVADOS (staff/
/// impersonación), case-insensitive y accent-insensitive, cuando el nickname
/// normalizado ES un token o lo CONTIENE como palabra completa. Los errores
/// salen como <c>ValidationException</c> → 400 con errores por propiedad.
/// </summary>
public sealed class UpdateLeaguePreferencesCommandValidator
    : AbstractValidator<UpdateLeaguePreferencesCommand>
{
    // Charset del nickname: alfanumérico ASCII + espacio/guion/guion bajo +
    // vocales acentuadas y ñ/ü (ES, convención del módulo). Sin emojis ni
    // símbolos (evita ruido visual y confusiones de display en la liga).
    private const string NicknamePattern = "^[A-Za-z0-9 _\\-áéíóúñüÁÉÍÓÚÑÜ]+$";

    /// <summary>
    /// Tokens reservados: suplantación de staff/marca (LEAGUE v1). La
    /// comparación es case-insensitive y accent-insensitive (normalización
    /// NFKD + sin marcas diacríticas antes de comparar).
    /// </summary>
    private static readonly IReadOnlySet<string> ReservedTokens =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "admin", "administrador", "soporte", "support", "seguro", "aseguradora",
            "doctor", "dr", "dra", "enfermera", "enfermero", "nutricionista",
            "psicologo", "psicologa", "coach", "antares", "copp", "adresd", "coppadresd",
        };

    public UpdateLeaguePreferencesCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("El patientId es requerido.");

        RuleFor(x => x.Nickname)
            .Must(n => n is null || IsValidNickname(n))
            .WithMessage(
                "El nickname debe tener entre 3 y 32 caracteres (tras recortar) "
                    + "y solo letras (incl. acentuadas), números, espacios, guiones o guion bajo."
            );

        RuleFor(x => x.Nickname)
            .Must(n => n is null || !ContainsReservedToken(n))
            .WithMessage("Ese apodo no está disponible.");

        RuleFor(x => x.Nickname)
            .NotEmpty()
            .When(x => x.OptIn)
            .WithMessage("Para participar en la liga se requiere un nickname (3-32 caracteres).");
    }

    private static bool IsValidNickname(string nickname)
    {
        var trimmed = nickname.Trim();
        return trimmed.Length is >= 3 and <= 32
            && Regex.IsMatch(trimmed, NicknamePattern);
    }

    /// <summary>
    /// Verdadero si el nickname normalizado (minúsculas, sin acentos) ES un
    /// token reservado o lo contiene como palabra COMPLETA (límites no
    /// alfanuméricos). "Dragón FC" no contiene "dr" como palabra (substring
    /// no cuenta); "Administrador de fincas" sí contiene "administrador".
    /// </summary>
    private static bool ContainsReservedToken(string nickname)
    {
        var normalized = Normalize(nickname.Trim());
        if (ReservedTokens.Contains(normalized))
        {
            return true;
        }

        foreach (var word in Regex.Split(normalized, "[^a-z0-9]+"))
        {
            if (word.Length > 0 && ReservedTokens.Contains(word))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Minúsculas + sin diacríticos (NFKD y se descartan las marcas).</summary>
    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().ToLowerInvariant();
    }
}