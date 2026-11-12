namespace CoppAddresd.Domain.Entities;
/// <summary>Lectura congelada al activar; ObservedAt nunca se sustituye por la hora del envío.</summary>
public sealed record SosVitalReading(string MetricCode, decimal Value, string Unit, DateTime? ObservedAt, string Source);
