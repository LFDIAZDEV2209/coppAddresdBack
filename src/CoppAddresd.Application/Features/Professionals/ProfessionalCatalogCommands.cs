using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>
/// Comandos de gestión admin de catálogos de tipos de profesional y
/// especialidades (schema <c>erp</c>). Los códigos son inmutables una vez
/// creados (identidad del catálogo); el desactivado es soft (IsActive), nunca
/// se borran filas que puedan referenciar profesionales.
/// </summary>
public static class ProfessionalCatalogCommands
{
    public static string NormalizeCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 50)
        {
            throw new BusinessRuleViolationException(
                "El código es obligatorio y no puede superar 50 caracteres."
            );
        }

        if (!normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new BusinessRuleViolationException(
                "El código solo admite letras, números, guiones y guiones bajos."
            );
        }

        return normalized;
    }
}

/// <summary>Crea un tipo de profesional (código único, inmutable).</summary>
public record CreateProfessionalTypeCommand(
    string Code,
    string Name,
    string? Description,
    int SortOrder
) : IRequest<ProfessionalTypeDto>;

public sealed class CreateProfessionalTypeCommandHandler(
    IOrganizationRepository repository,
    ICacheService cache
) : IRequestHandler<CreateProfessionalTypeCommand, ProfessionalTypeDto>
{
    public async Task<ProfessionalTypeDto> Handle(
        CreateProfessionalTypeCommand request,
        CancellationToken ct
    )
    {
        var code = ProfessionalCatalogCommands.NormalizeCode(request.Code);
        if (await repository.ProfessionalTypeCodeExistsAsync(code, ct))
        {
            throw new BusinessRuleViolationException(
                $"Ya existe un tipo de profesional con el código '{code}'."
            );
        }

        var entity = new ProfessionalType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var created = await repository.AddProfessionalTypeAsync(entity, ct);
        // Invalidación del catálogo cacheado en el mismo flujo de escritura
        // (la siguiente lectura reconstruye con el cambio visible).
        await cache.RemoveAsync(CacheKeys.Catalog("professional-types"), ct);
        return ProfessionalTypeDto.FromEntity(created);
    }
}

/// <summary>
/// Actualiza un tipo de profesional (nombre, descripción, orden, activo).
/// El código no se toca (identidad del catálogo).
/// </summary>
public record UpdateProfessionalTypeCommand(
    Guid Id,
    string? Name,
    string? Description,
    int? SortOrder,
    bool? IsActive
) : IRequest<ProfessionalTypeDto?>;

public sealed class UpdateProfessionalTypeCommandHandler(
    IOrganizationRepository repository,
    ICacheService cache
) : IRequestHandler<UpdateProfessionalTypeCommand, ProfessionalTypeDto?>
{
    public async Task<ProfessionalTypeDto?> Handle(
        UpdateProfessionalTypeCommand request,
        CancellationToken ct
    )
    {
        var entity = await repository.GetProfessionalTypeByIdAsync(request.Id, ct);
        if (entity is null)
        {
            return null;
        }

        if (request.Name is not null)
            entity.Name = request.Name.Trim();
        if (request.Description is not null)
            entity.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        if (request.SortOrder is not null)
            entity.SortOrder = request.SortOrder.Value;
        if (request.IsActive is not null)
            entity.IsActive = request.IsActive.Value;

        await repository.UpdateProfessionalTypeAsync(entity, ct);
        await cache.RemoveAsync(CacheKeys.Catalog("professional-types"), ct);
        return ProfessionalTypeDto.FromEntity(entity);
    }
}

/// <summary>Crea una especialidad (código único, inmutable).</summary>
public record CreateSpecialtyCommand(
    string Code,
    string Name,
    string Category,
    string? Description,
    int SortOrder
) : IRequest<SpecialtyDto>;

public sealed class CreateSpecialtyCommandHandler(
    IOrganizationRepository repository,
    ICacheService cache
) : IRequestHandler<CreateSpecialtyCommand, SpecialtyDto>
{
    public async Task<SpecialtyDto> Handle(CreateSpecialtyCommand request, CancellationToken ct)
    {
        var code = ProfessionalCatalogCommands.NormalizeCode(request.Code);
        if (await repository.SpecialtyCodeExistsAsync(code, ct))
        {
            throw new BusinessRuleViolationException(
                $"Ya existe una especialidad con el código '{code}'."
            );
        }

        var entity = new Specialty
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var created = await repository.AddSpecialtyAsync(entity, ct);
        await cache.RemoveAsync(CacheKeys.Catalog("specialties"), ct);
        return SpecialtyDto.FromEntity(created);
    }
}

/// <summary>
/// Actualiza una especialidad (nombre, categoría, descripción, orden, activo).
/// El código no se toca (identidad del catálogo).
/// </summary>
public record UpdateSpecialtyCommand(
    Guid Id,
    string? Name,
    string? Category,
    string? Description,
    int? SortOrder,
    bool? IsActive
) : IRequest<SpecialtyDto?>;

public sealed class UpdateSpecialtyCommandHandler(
    IOrganizationRepository repository,
    ICacheService cache
) : IRequestHandler<UpdateSpecialtyCommand, SpecialtyDto?>
{
    public async Task<SpecialtyDto?> Handle(UpdateSpecialtyCommand request, CancellationToken ct)
    {
        var entity = await repository.GetSpecialtyByIdAsync(request.Id, ct);
        if (entity is null)
        {
            return null;
        }

        if (request.Name is not null)
            entity.Name = request.Name.Trim();
        if (request.Category is not null)
            entity.Category = request.Category.Trim();
        if (request.Description is not null)
            entity.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        if (request.SortOrder is not null)
            entity.SortOrder = request.SortOrder.Value;
        if (request.IsActive is not null)
            entity.IsActive = request.IsActive.Value;

        await repository.UpdateSpecialtyAsync(entity, ct);
        await cache.RemoveAsync(CacheKeys.Catalog("specialties"), ct);
        return SpecialtyDto.FromEntity(entity);
    }
}
