using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>Implementación EF del repositorio de solicitudes de telemedicina.</summary>
public sealed class RequestRepository(TelemedicineDbContext dbContext) : IRequestRepository
{
    public async Task<TelemedicineRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Requests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<TelemedicineRequest> AddAsync(
        TelemedicineRequest request,
        CancellationToken ct = default)
    {
        dbContext.Requests.Add(request);
        await dbContext.SaveChangesAsync(ct);
        return request;
    }

    public async Task UpdateAsync(
        TelemedicineRequest request,
        CancellationToken ct = default)
    {
        dbContext.Requests.Update(request);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(
        Guid requestId,
        AppointmentRequestStatus status,
        CancellationToken ct = default)
        => await dbContext.Requests
            .Where(r => r.Id == requestId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                ct);

    public async Task<IReadOnlyList<TelemedicineRequest>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default)
        => await dbContext.Requests.AsNoTracking()
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<TelemedicineRequest> Items, int Total)> ListByOrganizationAsync(
        Guid organizationId,
        AppointmentRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = dbContext.Requests.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId);

        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
