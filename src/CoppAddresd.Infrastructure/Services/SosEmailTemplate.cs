using System.Net;
using System.Text;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Correo SOS independiente del SMS, legible sin imágenes remotas.</summary>
public static class SosEmailTemplate
{
    // Sin PII en el asunto: los transportes pueden registrarlo para diagnóstico.
    public const string Subject = "Alerta SOS | Copp Adresd";

    public static string BuildHtml(SosAlert alert, string? locationUrl = null)
    {
        var name = Encode(SosMessageFormatting.FullName(alert.Patient, "Tu contacto"));
        var firstName = Encode(SosMessageFormatting.FirstName(alert.Patient, "tu contacto"));
        var time = Encode(SosMessageFormatting.FormatMessageTime(alert.CreatedAt));
        var phone = SosMessageFormatting.PatientPhone(alert.Patient);
        var location = SosMessageFormatting.LocationLink(alert, locationUrl);
        var callAction = phone is null
            ? "<p style=\"margin:0 0 18px;color:#53657f;font-size:15px;line-height:1.6\">Usa el número de contacto que tengas registrado para comunicarte de inmediato.</p>"
            : Button("tel:" + phone, "Llamar a " + SosMessageFormatting.FirstName(alert.Patient, "tu contacto"), "#142855");
        var locationContent = location is null
            ? $$"""
                <p style="margin:0;color:#142855;font-size:16px;font-weight:700">Ubicación no compartida</p>
                <p style="margin:7px 0 0;color:#53657f;font-size:14px;line-height:1.6">Contacta a {{firstName}} para saber dónde se encuentra.</p>
                """
            : $$"""
                <p style="margin:0 0 7px;color:#142855;font-size:16px;font-weight:700">Ubicación compartida</p>
                <p style="margin:0 0 16px;color:#53657f;font-size:14px;line-height:1.6">Abre el mapa para consultar la ubicación enviada al activar la alerta.</p>
                {{Button(location, "Ver ubicación en el mapa", "#035d4d")}}
                """;
        var readings = SosMessageFormatting.ReportedVitals(alert);
        var readingsContent = new StringBuilder();
        if (readings.Count > 0)
        {
            readingsContent.Append("<tr><td style=\"padding:0 32px 28px\"><h2 style=\"margin:0 0 12px;color:#142855;font-size:16px\">Lecturas compartidas</h2><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse\">");
            foreach (var reading in readings)
            {
                readingsContent.Append($"<tr><td style=\"padding:10px 0;border-bottom:1px solid #e9edf3;color:#53657f;font-size:14px\">{Encode(reading.Label)}</td><td align=\"right\" style=\"padding:10px 0;border-bottom:1px solid #e9edf3;color:#142855;font-size:16px;font-weight:700\">{Encode(reading.Value)}</td></tr>");
            }
            readingsContent.Append("</table><p style=\"margin:10px 0 0;color:#53657f;font-size:12px;line-height:1.5\">Lecturas enviadas con la alerta.</p></td></tr>");
        }
        return $$"""
            <!doctype html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Alerta SOS · Copp Adresd</title></head>
            <body style="margin:0;padding:0;background:#f3f6f8;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0">{{name}} activó una alerta SOS. Comunícate ahora para ofrecerle ayuda.</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f3f6f8;border-collapse:collapse">
                <tr><td align="center" style="padding:24px 12px">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-collapse:separate;border-spacing:0;border:1px solid #dfe6ee;border-radius:20px;overflow:hidden">
                    <tr><td style="padding:26px 32px;background:#142855;color:#ffffff">
                      <p style="margin:0;font-size:23px;font-weight:700;letter-spacing:-.4px">Copp Adresd</p>
                      <p style="margin:6px 0 0;font-size:13px;color:#dce6f2">Tu salud, conectada</p>
                    </td></tr>
                    <tr><td style="padding:30px 32px 20px">
                      <p style="margin:0 0 18px;color:#b9463c;font-size:12px;font-weight:700;letter-spacing:1.5px">ALERTA SOS</p>
                      <h1 style="margin:0 0 12px;color:#142855;font-size:28px;line-height:1.2;letter-spacing:-.6px">{{name}} necesita tu ayuda</h1>
                      <p style="margin:0 0 12px;color:#53657f;font-size:16px;line-height:1.65">Activó una alerta desde Copp Adresd. Comunícate ahora para confirmar cómo se encuentra y ofrecerle ayuda.</p>
                      <p style="margin:0 0 22px;color:#53657f;font-size:13px;line-height:1.5">Alerta activada · {{time}}</p>
                      {{callAction}}
                    </td></tr>
                    <tr><td style="padding:0 32px 28px">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef5f3;border:1px solid #d8e8e2;border-radius:14px"><tr><td style="padding:20px">{{locationContent}}</td></tr></table>
                    </td></tr>
                    {{readingsContent}}
                    <tr><td style="padding:22px 32px;background:#f7f9fb;border-top:1px solid #e9edf3">
                      <p style="margin:0;color:#53657f;font-size:13px;line-height:1.6">Recibes este aviso porque eres su contacto de emergencia. Si necesita atención inmediata, comunícate con los servicios de emergencia de su ubicación.</p>
                      <p style="margin:12px 0 0;color:#53657f;font-size:12px;line-height:1.5">Copp Adresd</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    /// <summary>Versión legible para clientes sin HTML o integraciones de transporte futuras.</summary>
    public static string BuildText(SosAlert alert, string? locationUrl = null)
    {
        var text = new StringBuilder(SosSmsTemplate.Build(alert, locationUrl));
        text.Append("\n\nAlerta activada: ").Append(SosMessageFormatting.FormatMessageTime(alert.CreatedAt));
        text.Append("\nRecibes este aviso porque eres su contacto de emergencia.");
        return text.ToString();
    }

    private static string Button(string href, string label, string color) =>
        $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:separate\"><tr><td align=\"center\" bgcolor=\"{color}\" style=\"border-radius:12px\"><a href=\"{Encode(href)}\" style=\"display:block;padding:16px 20px;color:#ffffff;font-size:16px;font-weight:700;text-decoration:none;line-height:1.3\">{Encode(label)}</a></td></tr></table>";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
