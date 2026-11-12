using System.Net;
using System.Text;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Correo SOS compacto, legible sin imágenes remotas.</summary>
public static class SosEmailTemplate
{
    // Sin PII en el asunto: los transportes pueden registrarlo para diagnóstico.
    public const string Subject = "Alerta SOS | Copp Adresd";

    public static string BuildHtml(SosAlert alert, string? locationUrl = null)
    {
        var name = Encode(SosMessageFormatting.FullName(alert.Patient, "Tu contacto"));
        var time = Encode(SosMessageFormatting.FormatMessageTime(alert.CreatedAt));
        var phone = SosMessageFormatting.PatientPhone(alert.Patient);
        var location = SosMessageFormatting.LocationLink(alert, locationUrl);
        var call = phone is null
            ? "<p style=\"margin:0;color:#53657f;font-size:14px;line-height:1.5\">Usa el número de contacto que tengas registrado para comunicarte.</p>"
            : Button("tel:" + phone, "Llamar a " + SosMessageFormatting.FirstName(alert.Patient, "tu contacto"), "#142855");
        var locationContent = location is null
            ? "<p style=\"margin:0;color:#142855;font-size:15px;font-weight:700\">Ubicación no compartida</p>"
            : $$"""
              <p style="margin:0 0 7px;color:#035d4d;font-size:12px;font-weight:700">{{(string.IsNullOrWhiteSpace(alert.LocationDescription) ? "Dirección no disponible" : "Dirección")}}</p>
              <p style="margin:0 0 10px;color:#142855;font-size:17px;font-weight:700;line-height:1.4;overflow-wrap:anywhere;word-break:break-word">{{Encode(alert.LocationDescription ?? "Consulta el punto compartido en el mapa.")}}</p>
              <p style="margin:0 0 12px;color:#53657f;font-size:11px;line-height:1.6">{{Encode(SosMessageFormatting.LocationCaptureTime(alert))}}<br>{{Encode(SosMessageFormatting.LocationAccuracy(alert))}}</p>
              {{Button(location, "Ver ubicación en el mapa", "#035d4d")}}
              """;
        var readings = SosMessageFormatting.ReportedVitals(alert);
        var readingsContent = new StringBuilder();
        if (readings.Count == 0)
            readingsContent.Append("<p style=\"margin:0;color:#53657f;font-size:13px\">Sin lecturas compartidas.</p>");
        else
        {
            readingsContent.Append("<h2 style=\"margin:0 0 6px;color:#142855;font-size:14px\">Últimas lecturas registradas</h2><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;table-layout:fixed\">");
            foreach (var reading in readings)
                readingsContent.Append($"<tr><td style=\"padding:7px 4px 7px 0;border-bottom:1px solid #edf1f5;color:#53657f;font-size:13px\">{Encode(reading.Label)}<br><span style=\"font-size:10px;line-height:1.6\">{Encode(SosMessageFormatting.VitalProvenance(alert, reading.Label))}</span></td><td align=\"right\" style=\"padding:7px 0;border-bottom:1px solid #edf1f5;color:#142855;font-size:14px;font-weight:700;overflow-wrap:anywhere\">{Encode(reading.Value)}</td></tr>");
            readingsContent.Append("</table>");
            var missing = SosMessageFormatting.MissingVitals(alert);
            if (missing.Length > 0) readingsContent.Append("<p style=\"margin:8px 0 0;color:#53657f;font-size:11px\">No disponibles: " + Encode(string.Join(", ", missing)) + ".</p>");
        }
        var mapNote = location is null ? "" : "Ubicación de la alerta, sin seguimiento en tiempo real. "
            + (location == locationUrl ? "Enlace privado: 24 horas o hasta cancelar la alerta." : "Confirma la dirección al comunicarte.");
        var attribution = location is not null && !string.IsNullOrWhiteSpace(alert.LocationDescription)
            ? "<br>Dirección: Amazon Location · <a href=\"https://docs.aws.amazon.com/location/latest/developerguide/data-attribution.html\" style=\"color:#53657f\">Fuentes de datos</a>" : "";
        return $$"""
            <!doctype html><html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Alerta SOS · Copp Adresd</title></head>
            <body style="margin:0;padding:0;background:#f1f5f8;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0">{{name}} necesita tu ayuda.</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f1f5f8;border-collapse:collapse"><tr><td align="center" style="padding:20px 12px">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;table-layout:fixed;background:#ffffff;border:1px solid #dfe6ee;border-top:4px solid #035d4d;border-radius:16px;border-spacing:0;overflow:hidden">
                  <tr><td style="padding:22px 22px 18px">
                    <p style="margin:0 0 20px;color:#142855;font-size:17px;font-weight:700">Copp Adresd</p>
                    <p style="margin:0 0 10px;color:#aa372f;font-size:11px;font-weight:700;letter-spacing:1px">ALERTA SOS</p>
                    <h1 style="margin:0 0 10px;color:#142855;font-size:24px;line-height:1.3;letter-spacing:-.4px;overflow-wrap:anywhere;word-break:break-word">{{name}} necesita tu ayuda</h1>
                    <p style="margin:0 0 10px;color:#53657f;font-size:14px;line-height:1.6">Comunícate ahora para confirmar cómo se encuentra.</p>
                    <p style="margin:0 0 18px;color:#53657f;font-size:11px;line-height:1.5">Alerta activada · {{time}}</p>
                    {{call}}
                  </td></tr>
                  <tr><td style="padding:0 22px 18px">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="table-layout:fixed;background:#eff7f4;border-radius:12px"><tr><td style="padding:16px">{{locationContent}}</td></tr></table>
                  </td></tr>
                  <tr><td style="padding:0 22px 20px">{{readingsContent}}</td></tr>
                  <tr><td style="padding:16px 22px;border-top:1px solid #edf1f5">
                    <p style="margin:0;color:#53657f;font-size:11px;line-height:1.6">Recibes este aviso porque eres su contacto de emergencia. Datos reportados al activar la alerta. {{mapNote}}{{attribution}}</p>
                  </td></tr>
                </table>
              </td></tr></table>
            </body></html>
            """;
    }

    /// <summary>Versión completa para clientes sin HTML; no hereda el recorte del SMS.</summary>
    public static string BuildText(SosAlert alert, string? locationUrl = null)
    {
        var text = new StringBuilder("Alerta SOS: ")
            .Append(SosMessageFormatting.FullName(alert.Patient, "Tu contacto")).Append(" necesita tu ayuda.\nCopp Adresd\n")
            .Append("Alerta activada: ").Append(SosMessageFormatting.FormatMessageTime(alert.CreatedAt));
        var phone = SosMessageFormatting.PatientPhone(alert.Patient);
        if (phone is not null) text.Append("\nLlamar: ").Append(phone);
        var location = SosMessageFormatting.LocationLink(alert, locationUrl);
        if (location is null) text.Append("\nUbicación no compartida.");
        else
        {
            text.Append("\n").Append(string.IsNullOrWhiteSpace(alert.LocationDescription) ? "Dirección no disponible." : "Dirección: " + alert.LocationDescription)
                .Append('\n').Append(SosMessageFormatting.LocationCaptureTime(alert))
                .Append('\n').Append(SosMessageFormatting.LocationAccuracy(alert))
                .Append("\nMapa: ").Append(location)
                .Append("\nUbicación de la alerta, sin seguimiento en tiempo real.");
            if (location == locationUrl) text.Append("\nEnlace privado: 24 horas o hasta cancelar la alerta.");
            if (!string.IsNullOrWhiteSpace(alert.LocationDescription)) text.Append("\nDirección: Amazon Location · https://docs.aws.amazon.com/location/latest/developerguide/data-attribution.html");
        }
        var readings = SosMessageFormatting.ReportedVitals(alert);
        text.Append(readings.Count == 0 ? "\nSin lecturas compartidas." : "\nÚltimas lecturas registradas:");
        foreach (var reading in readings) text.Append('\n').Append(reading.Label).Append(": ").Append(reading.Value).Append(" · ").Append(SosMessageFormatting.VitalProvenance(alert, reading.Label));
        if (readings.Count > 0 && SosMessageFormatting.MissingVitals(alert) is { Length: > 0 } missing)
            text.Append("\nNo disponibles: ").Append(string.Join(", ", missing)).Append('.');
        return text.Append("\nRecibes este aviso porque eres su contacto de emergencia.").ToString();
    }

    private static string Button(string href, string label, string color) =>
        $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"table-layout:fixed;border-collapse:separate\"><tr><td align=\"center\" bgcolor=\"{color}\" style=\"border-radius:10px\"><a href=\"{Encode(href)}\" style=\"display:block;padding:13px 12px;color:#ffffff;font-size:14px;font-weight:700;text-decoration:none;line-height:1.4;overflow-wrap:anywhere;word-break:break-word\">{Encode(label)}</a></td></tr></table>";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
