using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Services;
namespace CoppAddresd.Infrastructure.Sos;

/// <summary>Texto estático coincide con el alta de plantilla Meta. Parámetros siempre de snapshot.</summary>
public static class SosWhatsAppTemplate
{
    public const string Body = "Alerta SOS: {{1}} necesita tu ayuda.\n\nDirección: {{2}}\nMapa: {{3}}\nAlerta: {{4}}\n\nÚltimas lecturas registradas: {{5}}\n\nComunícate ahora para confirmar cómo se encuentra. Recibes este aviso porque eres su contacto de emergencia. Copp Adresd.";
    public static string[] Parameters(SosAlert alert, string patientName, string? link)
    {
        var readings=SosMessageFormatting.ReportedVitals(alert);
        var vitals=readings.Count==0 ? "Sin lecturas disponibles" : string.Join("; ",readings.Select(r=>$"{r.Label}: {r.Value} ({SosMessageFormatting.VitalProvenance(alert,r.Label)})"));
        var missing=SosMessageFormatting.MissingVitals(alert);
        if(readings.Count>0 && missing.Length>0) vitals+=". No disponibles: "+string.Join(", ",missing);
        var map=SosMessageFormatting.LocationLink(alert,link);
        var address=map is null ? "Ubicación no compartida" : string.IsNullOrWhiteSpace(alert.LocationDescription) ? "Dirección no disponible" : alert.LocationDescription;
        return [Clean(patientName),Clean(address),map??"Ubicación no compartida",SosMessageFormatting.FormatMessageTime(alert.CreatedAt),Clean(vitals)];
    }
    // Meta no admite saltos de línea/tabs dentro de parámetros de texto.
    private static string Clean(string value)=>SosMessageFormatting.SingleLine(value).Replace("*","").Replace("_","").Replace("~","").Replace("`","");
    public static string Render(IReadOnlyList<string> values) {var text=Body;for(var i=0;i<values.Count;i++)text=text.Replace("{{"+(i+1)+"}}",values[i]);return text;}
}
