using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Inbox por usuario y outbox de entrega; únicos y claims impiden duplicar avisos por fallback/replay.</summary>
public sealed class ProgramAssignmentNoticeRepository(AppDbContext db) : IProgramAssignmentNoticeRepository
{
    public async Task AddProfessionalNoticesAsync(Guid recommendationId, Guid patientId, DateTime utcNow, CancellationToken ct)
    {
        var patientName = await db.PatientProfiles.Where(p=>p.Id==patientId).Select(p=>p.FirstName+" "+p.LastName).SingleAsync(ct);
        var users = await (from pp in db.PatientProfessionalAssignments.AsNoTracking()
            join professional in db.Professionals.AsNoTracking() on pp.ProfessionalId equals professional.Id
            join employee in db.Employees.AsNoTracking() on professional.EmployeeId equals employee.Id
            where pp.PatientId==patientId && pp.Status=="Active" && employee.UserId!=null && employee.DeletedAt==null && employee.Status=="Active"
            select employee.UserId!.Value).Distinct().ToListAsync(ct);
        var existing = await db.ProgramAssignmentNotices.Where(n=>n.RecommendationId==recommendationId && n.Kind=="professional-ready")
            .Select(n=>n.UserId).ToListAsync(ct);
        db.ProgramAssignmentNotices.AddRange(users.Except(existing).Select(user=>new ProgramAssignmentNotice
        {
            Id=Guid.NewGuid(), RecommendationId=recommendationId, UserId=user, Kind="professional-ready",
            TitleEs="Asignación de programa pendiente", TitleEn="Program assignment pending",
            MessageEs=$"El paciente {patientName} completó la evaluación inicial y espera asignación de programa",
            MessageEn=$"Patient {patientName} completed the initial assessment and is waiting for program assignment",
            Link=$"/program/gestion?view=perfil-360&patient={patientId}", CreatedAt=utcNow, NextAttemptAt=utcNow,
            // El inbox es el canal del ERP: ya está entregado al persistirse, no se envía push extra.
            DeliveredAt=utcNow, PushCompleted=true, ChatCompleted=true,
        }));
        await db.SaveChangesAsync(ct);
    }

    public async Task AddPatientNoticeAsync(Guid recommendationId, Guid patientId, DateTime utcNow, CancellationToken ct)
    {
        var user = await db.PatientProfiles.Where(p=>p.Id==patientId).Select(p=>p.UserId).SingleAsync(ct);
        if (user is null || await db.ProgramAssignmentNotices.AnyAsync(n=>n.RecommendationId==recommendationId
            && n.UserId==user.Value && n.Kind=="patient-ready",ct)) return;
        db.ProgramAssignmentNotices.Add(new ProgramAssignmentNotice
        {
            Id=Guid.NewGuid(), RecommendationId=recommendationId, UserId=user.Value, Kind="patient-ready",
            TitleEs="Tu plan personalizado está listo", TitleEn="Your personalized plan is ready",
            MessageEs="Tu plan personalizado está listo - empieza tu semana 1",
            MessageEn="Your personalized plan is ready - start your week 1", Link="/program",
            CreatedAt=utcNow, NextAttemptAt=utcNow,
        });
        // El repositorio de activación hará SaveChanges y commit junto con el estado clínico.
    }

    public async Task<IReadOnlyList<Guid>> ListPendingAsync(Guid? recommendationId, DateTime utcNow, int limit, CancellationToken ct) =>
        await db.ProgramAssignmentNotices.AsNoTracking().Where(n=>n.Kind=="patient-ready" && n.DeliveredAt==null
            && n.Attempts<5 && n.NextAttemptAt<=utcNow && (n.LeaseUntil==null || n.LeaseUntil<=utcNow)
            && (recommendationId==null || n.RecommendationId==recommendationId))
            .OrderBy(n=>n.NextAttemptAt).ThenBy(n=>n.Id).Take(Math.Clamp(limit,1,500)).Select(n=>n.Id).ToListAsync(ct);

    public async Task<ProgramAssignmentNotice?> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken ct)
    {
        var updated = await db.ProgramAssignmentNotices.Where(n=>n.Id==id && n.DeliveredAt==null && n.Attempts<5
            && n.NextAttemptAt<=utcNow && (n.LeaseUntil==null || n.LeaseUntil<=utcNow))
            .ExecuteUpdateAsync(s=>s.SetProperty(n=>n.LeaseUntil,utcNow.AddMinutes(15))
                .SetProperty(n=>n.Attempts,n=>n.Attempts+1),ct);
        return updated==0 ? null : await db.ProgramAssignmentNotices.AsNoTracking().SingleAsync(n=>n.Id==id,ct);
    }

    public async Task FinishAsync(Guid id, bool pushCompleted, bool chatCompleted, DateTime utcNow, CancellationToken ct)
    {
        await db.ProgramAssignmentNotices.Where(n=>n.Id==id && n.DeliveredAt==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(n=>n.PushCompleted,n=>n.PushCompleted || pushCompleted)
                .SetProperty(n=>n.ChatCompleted,n=>n.ChatCompleted || chatCompleted)
                .SetProperty(n=>n.DeliveredAt,n=>(n.PushCompleted || pushCompleted) && (n.ChatCompleted || chatCompleted) ? utcNow : (DateTime?)null)
                .SetProperty(n=>n.NextAttemptAt,utcNow.AddMinutes(5)).SetProperty(n=>n.LeaseUntil,(DateTime?)null),ct);
    }

    public async Task<AssignmentInboxPage> ListInboxAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var query=db.ProgramAssignmentNotices.AsNoTracking().Where(n=>n.UserId==userId && n.Kind=="professional-ready"
            && db.ProgramAssignmentRecommendations.Any(r=>r.Id==n.RecommendationId
                && db.PatientProfessionalAssignments.Any(pp=>pp.PatientId==r.PatientId && pp.Status=="Active"
                    && pp.Patient!.DeletedAt==null && pp.Professional!.Employee.UserId==userId
                    && pp.Professional.Employee.DeletedAt==null && pp.Professional.Employee.Status=="Active")));
        var total=await query.CountAsync(ct);
        var rows=await query.OrderByDescending(n=>n.CreatedAt).ThenBy(n=>n.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Join(db.ProgramAssignmentRecommendations, n=>n.RecommendationId, r=>r.Id,
                (n,r)=>new { Notice=n, r.PatientId }).ToListAsync(ct);
        // También corrige enlaces históricos al leer, sin reescribir avisos ni tocar su entrega.
        return new(rows.Select(row=>new AssignmentInboxItem(row.Notice.Id,row.Notice.RecommendationId,
            row.Notice.Kind,row.Notice.TitleEs,row.Notice.TitleEn,row.Notice.MessageEs,row.Notice.MessageEn,
            $"/program/gestion?view=perfil-360&patient={row.PatientId}",row.Notice.CreatedAt,row.Notice.ReadAt)).ToList(),total,page,pageSize);
    }
}
