namespace CoppAddresd.Telemedicine.Application.ReferenceData;
public record AppointmentSearchMatches(IReadOnlyList<Guid> PatientIds, IReadOnlyList<Guid> ProfessionalIds, IReadOnlyList<Guid> SpecialtyIds, IReadOnlyList<Guid> LocationIds);
