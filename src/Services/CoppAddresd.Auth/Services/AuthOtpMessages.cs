using System.Text.Encodings.Web;
using CoppAddresd.Auth.Interfaces;

namespace CoppAddresd.Auth.Services;

/// <summary>Idioma acotado de los mensajes OTP; los clientes anteriores conservan español.</summary>
public static class AuthMessageLanguage
{
    public static string Normalize(string? language)
    {
        var value = language?.Trim();
        return string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)
            || value?.StartsWith("en-", StringComparison.OrdinalIgnoreCase) == true ? "en" : "es";
    }
}

/// <summary>Texto transaccional de activación y recuperación, separado de los códigos y su validación.</summary>
public static class AuthOtpMessages
{
    public static EmailMessage Activation(string to, string firstName, string code, int lifetimeSeconds, string? language)
    {
        var locale = AuthMessageLanguage.Normalize(language);
        var nameHtml = HtmlEncoder.Default.Encode(firstName);
        var codeHtml = HtmlEncoder.Default.Encode(code);
        var minutes = lifetimeSeconds / 60;
        return locale == "en"
            ? new EmailMessage(to, "Your CoppAddresd verification code",
                $"<div lang=\"en\"><p>Hello {nameHtml},</p><p>Your verification code is <strong>{codeHtml}</strong>.</p><p>It expires in {minutes} minutes. If you did not request this code, ignore this email.</p></div>",
                $"Hello {firstName},\n\nYour verification code is {code}.\n\nIt expires in {minutes} minutes. If you did not request this code, ignore this email.")
            : new EmailMessage(to, "Tu código de verificación de CoppAddresd",
                $"<div lang=\"es\"><p>Hola {nameHtml},</p><p>Tu código de verificación es <strong>{codeHtml}</strong>.</p><p>Vence en {minutes} minutos. Si no solicitaste este código, ignora este correo.</p></div>",
                $"Hola {firstName},\n\nTu código de verificación es {code}.\n\nVence en {minutes} minutos. Si no solicitaste este código, ignora este correo.");
    }

    public static EmailMessage Recovery(string to, string code, int lifetimeSeconds, string? language)
    {
        var locale = AuthMessageLanguage.Normalize(language);
        var codeHtml = HtmlEncoder.Default.Encode(code);
        var minutes = lifetimeSeconds / 60;
        return locale == "en"
            ? new EmailMessage(to, "Reset your CoppAddresd password",
                $"<div lang=\"en\"><p>Your password reset code is <strong>{codeHtml}</strong>. It expires in {minutes} minutes.</p><p>If you did not request this, ignore this email.</p></div>",
                $"Your password reset code: {code}. It expires in {minutes} minutes. If you did not request this, ignore this email.")
            : new EmailMessage(to, "Recupera tu contraseña de CoppAddresd",
                $"<div lang=\"es\"><p>Tu código para cambiar tu contraseña es <strong>{codeHtml}</strong>. Vence en {minutes} minutos.</p><p>Si no lo solicitaste, ignora este correo.</p></div>",
                $"Código para cambiar tu contraseña: {code}. Vence en {minutes} minutos. Si no lo solicitaste, ignora este correo.");
    }
}
