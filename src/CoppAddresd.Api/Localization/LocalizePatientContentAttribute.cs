using CoppAddresd.Application.Services.Localization;

namespace CoppAddresd.Api.Localization;

/// <summary>Activa traducción solo en lecturas del paciente con campos permitidos explícitos.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalizePatientContentAttribute(PatientContentProfile profile) : Attribute
{
    public PatientContentProfile Profile { get; } = profile;
}
