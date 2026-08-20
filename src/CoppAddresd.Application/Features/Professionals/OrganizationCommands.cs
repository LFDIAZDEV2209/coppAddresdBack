using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Crea una organización (proveedor raíz del tenant).</summary>
public record CreateOrganizationCommand(string Code, string Name) : IRequest<OrganizationDto>;

public sealed class CreateOrganizationCommandHandler(
    IOrganizationRepository repository,
    ILogger<CreateOrganizationCommandHandler> logger) : IRequestHandler<CreateOrganizationCommand, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(CreateOrganizationCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        var existing = await repository.ListOrganizationsAsync(ct);
        if (existing.Any(o => o.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleViolationException($"Ya existe una organización con el código '{code}'.");

        var entity = new Organization
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddOrganizationAsync(entity, ct);

        logger.LogInformation("Organización creada: {Id} ({Name})", entity.Id, entity.Name);
        return OrganizationDto.FromEntity(entity);
    }
}

/// <summary>Actualiza nombre o estado de una organización.</summary>
public record UpdateOrganizationCommand(Guid Id, string? Name, bool? IsActive) : IRequest<OrganizationDto?>;

public sealed class UpdateOrganizationCommandHandler(
    IOrganizationRepository repository,
    ILogger<UpdateOrganizationCommandHandler> logger) : IRequestHandler<UpdateOrganizationCommand, OrganizationDto?>
{
    public async Task<OrganizationDto?> Handle(UpdateOrganizationCommand request, CancellationToken ct)
    {
        var entity = await repository.GetOrganizationByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        if (request.Name is not null)
            entity.Name = request.Name.Trim();
        if (request.IsActive is not null)
            entity.IsActive = request.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateOrganizationAsync(entity, ct);

        logger.LogInformation("Organización actualizada: {Id}", entity.Id);
        return OrganizationDto.FromEntity(entity);
    }
}

/// <summary>Crea una clínica bajo una organización.</summary>
public record CreateClinicCommand(Guid OrganizationId, string Name, string? Code) : IRequest<ClinicDto>;

public sealed class CreateClinicCommandHandler(
    IOrganizationRepository repository,
    ILogger<CreateClinicCommandHandler> logger) : IRequestHandler<CreateClinicCommand, ClinicDto>
{
    public async Task<ClinicDto> Handle(CreateClinicCommand request, CancellationToken ct)
    {
        var organization = await repository.GetOrganizationByIdAsync(request.OrganizationId, ct)
            ?? throw new UnprocessableEntityException("La organización no existe.");

        var entity = new Clinic
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = request.Name.Trim(),
            Code = ProfessionalOptions.Normalize(request.Code),
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddClinicAsync(entity, ct);

        logger.LogInformation("Clínica creada: {Id} ({Name}) en organización {OrganizationId}",
            entity.Id, entity.Name, organization.Id);
        return ClinicDto.FromEntity(entity);
    }
}

/// <summary>Actualiza datos de una clínica.</summary>
public record UpdateClinicCommand(Guid Id, string? Name, string? Code, bool? IsActive) : IRequest<ClinicDto?>;

public sealed class UpdateClinicCommandHandler(
    IOrganizationRepository repository,
    ILogger<UpdateClinicCommandHandler> logger) : IRequestHandler<UpdateClinicCommand, ClinicDto?>
{
    public async Task<ClinicDto?> Handle(UpdateClinicCommand request, CancellationToken ct)
    {
        var entity = await repository.GetClinicByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        if (request.Name is not null)
            entity.Name = request.Name.Trim();
        if (request.Code is not null)
            entity.Code = ProfessionalOptions.Normalize(request.Code);
        if (request.IsActive is not null)
            entity.IsActive = request.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateClinicAsync(entity, ct);

        logger.LogInformation("Clínica actualizada: {Id}", entity.Id);
        return ClinicDto.FromEntity(entity);
    }
}

/// <summary>Crea una sede bajo una clínica.</summary>
public record CreateLocationCommand(
    Guid ClinicId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    Guid? CityId,
    Guid? StateId,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber) : IRequest<LocationDto>;

public sealed class CreateLocationCommandHandler(
    IOrganizationRepository repository,
    ILogger<CreateLocationCommandHandler> logger) : IRequestHandler<CreateLocationCommand, LocationDto>
{
    public async Task<LocationDto> Handle(CreateLocationCommand request, CancellationToken ct)
    {
        var clinic = await repository.GetClinicByIdAsync(request.ClinicId, ct)
            ?? throw new UnprocessableEntityException("La clínica no existe.");

        var entity = new Location
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            Name = request.Name.Trim(),
            AddressLine1 = ProfessionalOptions.Normalize(request.AddressLine1),
            AddressLine2 = ProfessionalOptions.Normalize(request.AddressLine2),
            CityId = request.CityId,
            StateId = request.StateId,
            PostalCode = ProfessionalOptions.Normalize(request.PostalCode),
            PhoneCountryCode = ProfessionalOptions.Normalize(request.PhoneCountryCode),
            PhoneNumber = ProfessionalOptions.Normalize(request.PhoneNumber),
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddLocationAsync(entity, ct);

        logger.LogInformation("Sede creada: {Id} ({Name}) en clínica {ClinicId}",
            entity.Id, entity.Name, clinic.Id);
        return LocationDto.FromEntity(entity);
    }
}

/// <summary>Actualiza datos de una sede.</summary>
public record UpdateLocationCommand(
    Guid Id,
    string? Name,
    string? AddressLine1,
    string? AddressLine2,
    Guid? CityId,
    Guid? StateId,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber,
    bool? IsActive) : IRequest<LocationDto?>;

public sealed class UpdateLocationCommandHandler(
    IOrganizationRepository repository,
    ILogger<UpdateLocationCommandHandler> logger) : IRequestHandler<UpdateLocationCommand, LocationDto?>
{
    public async Task<LocationDto?> Handle(UpdateLocationCommand request, CancellationToken ct)
    {
        var entity = await repository.GetLocationByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        if (request.Name is not null)
            entity.Name = request.Name.Trim();
        entity.AddressLine1 = ProfessionalOptions.Normalize(request.AddressLine1);
        entity.AddressLine2 = ProfessionalOptions.Normalize(request.AddressLine2);
        entity.CityId = request.CityId;
        entity.StateId = request.StateId;
        entity.PostalCode = ProfessionalOptions.Normalize(request.PostalCode);
        entity.PhoneCountryCode = ProfessionalOptions.Normalize(request.PhoneCountryCode);
        entity.PhoneNumber = ProfessionalOptions.Normalize(request.PhoneNumber);
        if (request.IsActive is not null)
            entity.IsActive = request.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateLocationAsync(entity, ct);

        logger.LogInformation("Sede actualizada: {Id}", entity.Id);
        return LocationDto.FromEntity(entity);
    }
}
