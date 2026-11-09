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
            ? "<p style=\"margin:0;color:#53657f;font-size:15px;line-height:1.6\">Usa el número de contacto que tengas registrado para comunicarte de inmediato.</p>"
            : Button("tel:" + phone, "Llamar a " + SosMessageFormatting.FirstName(alert.Patient, "tu contacto"), "#142855")
                + $"<p style=\"margin:10px 0 0;text-align:center;color:#53657f;font-size:13px\">{Encode(phone)}</p>";
        var accessNotice = location == locationUrl && location is not null
            ? "Enlace privado: disponible durante 24 horas desde la alerta. Deja de funcionar si se cancela."
            : "Consulta el punto enviado con esta alerta.";
        var locationContent = location is null
            ? $$"""
                <p style="margin:0;color:#142855;font-size:18px;font-weight:700">Ubicación no compartida</p>
                <p style="margin:8px 0 0;color:#53657f;font-size:14px;line-height:1.6">Contacta a {{firstName}} para saber dónde se encuentra.</p>
                """
            : $$"""
                <p style="margin:0 0 8px;color:#035d4d;font-size:12px;font-weight:700;letter-spacing:.6px">{{(string.IsNullOrWhiteSpace(alert.LocationDescription) ? "Dirección no disponible" : "Dirección aproximada")}}</p>
                <p style="margin:0 0 16px;color:#142855;font-size:20px;font-weight:700;line-height:1.45;overflow-wrap:anywhere;word-break:break-word">{{Encode(alert.LocationDescription ?? "Consulta el punto compartido en el mapa.")}}</p>
                <p style="margin:0 0 6px;color:#53657f;font-size:13px;line-height:1.6">{{Encode(SosMessageFormatting.LocationCaptureTime(alert))}}</p>
                <p style="margin:0 0 16px;color:#53657f;font-size:13px;line-height:1.6">{{Encode(SosMessageFormatting.LocationAccuracy(alert))}}</p>
                {{Button(location, "Ver ubicación en el mapa", "#035d4d")}}
                <p style="margin:14px 0 0;color:#53657f;font-size:12px;line-height:1.6">Es el punto enviado con esta alerta; no es seguimiento en tiempo real. Confirma la dirección con {{firstName}}.</p>
                <p style="margin:8px 0 0;color:#53657f;font-size:12px;line-height:1.6">{{accessNotice}}</p>
                {{(string.IsNullOrWhiteSpace(alert.LocationDescription) ? "" : "<p style=\"margin:12px 0 0;font-size:11px;color:#53657f;line-height:1.5\">Dirección: Amazon Location · <a href=\"https://docs.aws.amazon.com/location/latest/developerguide/data-attribution.html\" style=\"color:#53657f\">Fuentes de datos</a></p>")}}
                """;
        var readings = SosMessageFormatting.ReportedVitals(alert);
        var readingsContent = new StringBuilder();
        if (readings.Count == 0)
        {
            readingsContent.Append("<p style=\"margin:0;color:#142855;font-size:15px;font-weight:700\">Sin lecturas compartidas</p><p style=\"margin:7px 0 0;color:#53657f;font-size:13px;line-height:1.6\">Esta alerta no incluye signos vitales. Confirma cómo se encuentra al comunicarte.</p>");
        }
        else
        {
            readingsContent.Append("<h2 style=\"margin:0 0 12px;color:#142855;font-size:16px\">Lecturas compartidas</h2><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse\">");
            foreach (var reading in readings)
                readingsContent.Append($"<tr><td style=\"padding:10px 4px 10px 0;border-bottom:1px solid #e9edf3;color:#53657f;font-size:14px\">{Encode(reading.Label)}</td><td align=\"right\" style=\"padding:10px 0;border-bottom:1px solid #e9edf3;color:#142855;font-size:16px;font-weight:700;overflow-wrap:anywhere\">{Encode(reading.Value)}</td></tr>");
            readingsContent.Append("</table><p style=\"margin:10px 0 0;color:#53657f;font-size:12px;line-height:1.5\">Valores reportados al activar la alerta; no son mediciones en tiempo real.</p>");
        }
        return $$"""
            <!doctype html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Alerta SOS · Copp Adresd</title></head>
            <body style="margin:0;padding:0;background:#f1f5f8;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0">{{name}} necesita ayuda. Comunícate y consulta la ubicación compartida.</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f1f5f8;border-collapse:collapse">
                <tr><td align="center" style="padding:24px 12px">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;table-layout:fixed;background:#ffffff;border:1px solid #dfe6ee;border-radius:20px;border-spacing:0;overflow:hidden">
                    <tr><td style="padding:24px;border-bottom:1px solid #e9edf3">
                      <p style="margin:0;color:#142855;font-size:22px;font-weight:700;letter-spacing:-.4px">Copp Adresd</p>
                      <p style="margin:5px 0 0;color:#53657f;font-size:12px">Tu red de ayuda, conectada</p>
                    </td></tr>
                    <tr><td style="padding:28px 24px 24px">
                      <p style="margin:0 0 16px"><span style="display:inline-block;padding:7px 11px;background:#fff0ed;color:#a9342d;border-radius:6px;font-size:11px;font-weight:700;letter-spacing:1px">ALERTA SOS · NECESITA AYUDA</span></p>
                      <h1 style="margin:0 0 14px;color:#142855;font-size:28px;line-height:1.25;letter-spacing:-.6px;overflow-wrap:anywhere;word-break:break-word">{{name}} necesita tu ayuda</h1>
                      <p style="margin:0 0 16px;color:#53657f;font-size:15px;line-height:1.65">Activó una alerta desde Copp Adresd y tú eres parte de su red de emergencia. Comunícate ahora para confirmar cómo se encuentra.</p>
                      <p style="margin:0 0 22px;color:#53657f;font-size:12px;line-height:1.5">Alerta activada · {{time}}</p>
                      {{callAction}}
                    </td></tr>
                    <tr><td style="padding:0 24px 24px">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="table-layout:fixed;background:#eef6f3;border:1px solid #d8e8e2;border-radius:14px"><tr><td style="padding:20px">{{locationContent}}</td></tr></table>
                    </td></tr>
                    <tr><td style="padding:0 24px 24px">
                      <h2 style="margin:0 0 16px;color:#142855;font-size:18px">Qué hacer ahora</h2>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="table-layout:fixed;color:#53657f;font-size:14px;line-height:1.6">
                        {{Step("1", "Comunícate", "Llama o escribe para confirmar qué necesita y dónde se encuentra.")}}
                        {{Step("2", "Revisa la ubicación", location is null ? "Pídele una dirección o un punto de referencia." : "Abre el mapa y confirma la dirección antes de desplazarte.")}}
                        {{Step("3", "Busca ayuda si la necesita", "Si requiere atención inmediata, llama a los servicios de emergencia de su ubicación.")}}
                      </table>
                    </td></tr>
                    <tr><td style="padding:0 24px 24px">{{readingsContent}}</td></tr>
                    <tr><td style="padding:20px 24px;background:#f7f9fb;border-top:1px solid #e9edf3">
                      <p style="margin:0;color:#53657f;font-size:12px;line-height:1.65">Recibes este aviso porque eres su contacto de emergencia. La información corresponde al momento de la alerta.</p>
                      <p style="margin:10px 0 0;color:#142855;font-size:12px;font-weight:700">Copp Adresd · Cuidarnos es estar cerca</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    /// <summary>Versión completa para clientes sin HTML; no hereda el recorte del SMS.</summary>
    public static string BuildText(SosAlert alert, string? locationUrl = null)
    {
        var text = new StringBuilder("ALERTA SOS · Copp Adresd\n")
            .Append(SosMessageFormatting.FullName(alert.Patient, "Tu contacto")).Append(" necesita tu ayuda.\n")
            .Append("Alerta activada: ").Append(SosMessageFormatting.FormatMessageTime(alert.CreatedAt));
        var phone = SosMessageFormatting.PatientPhone(alert.Patient);
        text.Append(phone is null ? "\nUsa el número de contacto que tengas registrado." : "\nLlamar: " + phone);
        var location = SosMessageFormatting.LocationLink(alert, locationUrl);
        if (location is null) text.Append("\n\nUbicación no compartida. Confirma dónde se encuentra.");
        else
        {
            text.Append("\n\n").Append(alert.LocationDescription is null ? "Dirección no disponible." : "Dirección aproximada: " + alert.LocationDescription)
                .Append('\n').Append(SosMessageFormatting.LocationCaptureTime(alert))
                .Append('\n').Append(SosMessageFormatting.LocationAccuracy(alert))
                .Append("\nMapa: ").Append(location)
                .Append("\nNo es seguimiento en tiempo real. Confirma la dirección.");
            if (location == locationUrl) text.Append("\nEnlace privado: 24 horas desde la alerta; se revoca al cancelarla.");
            if (!string.IsNullOrWhiteSpace(alert.LocationDescription)) text.Append("\nDirección: Amazon Location · https://docs.aws.amazon.com/location/latest/developerguide/data-attribution.html");
        }
        text.Append("\n\nQué hacer ahora\n1. Comunícate para confirmar qué necesita.\n2. Confirma su ubicación.\n3. Si necesita atención inmediata, llama a los servicios de emergencia de su ubicación.");
        var readings = SosMessageFormatting.ReportedVitals(alert);
        text.Append(readings.Count == 0 ? "\n\nSin lecturas compartidas." : "\n\nLecturas compartidas:");
        foreach (var reading in readings) text.Append('\n').Append(reading.Label).Append(": ").Append(reading.Value);
        text.Append("\n\nRecibes este aviso porque eres su contacto de emergencia.");
        return text.ToString();
    }

    private static string Step(string number, string title, string detail) =>
        $"<tr><td valign=\"top\" width=\"30\" style=\"width:30px;padding:0 0 16px;color:#035d4d;font-size:16px;font-weight:700\">{number}.</td><td style=\"padding:0 0 16px;overflow-wrap:anywhere\"><strong style=\"color:#142855\">{title}</strong><br>{detail}</td></tr>";

    private static string Button(string href, string label, string color) =>
        $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"table-layout:fixed;border-collapse:separate\"><tr><td align=\"center\" bgcolor=\"{color}\" style=\"border-radius:12px\"><a href=\"{Encode(href)}\" style=\"display:block;padding:16px 14px;color:#ffffff;font-size:16px;font-weight:700;text-decoration:none;line-height:1.4;overflow-wrap:anywhere;word-break:break-word\">{Encode(label)}</a></td></tr></table>";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
