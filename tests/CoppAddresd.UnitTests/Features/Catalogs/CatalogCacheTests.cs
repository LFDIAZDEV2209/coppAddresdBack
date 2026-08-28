using CoppAddresd.Application.Features.Catalogs;
using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.UnitTests.Cache;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Catalogs;

/// <summary>
/// Comportamiento de caché de catálogos del ERP (SPEC infra/cache): lectura
/// calentada (miss → hit, una sola query) e invalidación en la mutación del
/// catálogo de profesionales (Create/Update → remove de la clave).
/// </summary>
public class CatalogCacheTests
{
    private readonly ICatalogRepository _catalogRepository = Substitute.For<ICatalogRepository>();
    private readonly IOrganizationRepository _organizationRepository =
        Substitute.For<IOrganizationRepository>();

    [Fact]
    public async Task BloodTypes_SegundaConsulta_EsHitConUnaSolaQuery()
    {
        var cache = new FakeCacheService();
        _catalogRepository
            .ListBloodTypesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new BloodType
                {
                    Id = Guid.NewGuid(),
                    Code = "O+",
                    Name = "O positivo",
                    SortOrder = 1,
                },
            ]);
        var handler = new ListBloodTypesQueryHandler(_catalogRepository, cache);

        var first = await handler.Handle(new ListBloodTypesQuery(), CancellationToken.None);
        var second = await handler.Handle(new ListBloodTypesQuery(), CancellationToken.None);

        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(first, second);
        await _catalogRepository.Received(1).ListBloodTypesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateProfessionalType_InvalidaLaClaveDelCatalogo()
    {
        var cache = new FakeCacheService();
        _organizationRepository
            .ProfessionalTypeCodeExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _organizationRepository
            .AddProfessionalTypeAsync(Arg.Any<ProfessionalType>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var entity = callInfo.Arg<ProfessionalType>();
                entity.Specialties = [];
                return entity;
            });
        var handler = new CreateProfessionalTypeCommandHandler(_organizationRepository, cache);

        await handler.Handle(
            new CreateProfessionalTypeCommand("NUT", "Nutricionista", null, 1),
            CancellationToken.None
        );

        Assert.Contains(CacheKeys.Catalog("professional-types"), cache.Removed);
    }

    [Fact]
    public async Task UpdateSpecialty_InvalidaLaClaveDelCatalogo()
    {
        var cache = new FakeCacheService();
        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            Code = "NUT_GEN",
            Name = "Nutrición general",
            Category = "Nutrición",
            SortOrder = 1,
            IsActive = true,
        };
        _organizationRepository
            .GetSpecialtyByIdAsync(specialty.Id, Arg.Any<CancellationToken>())
            .Returns(specialty);
        var handler = new UpdateSpecialtyCommandHandler(_organizationRepository, cache);

        await handler.Handle(
            new UpdateSpecialtyCommand(
                specialty.Id,
                Name: "Nutrición general actualizada",
                null,
                null,
                null,
                null
            ),
            CancellationToken.None
        );

        Assert.Contains(CacheKeys.Catalog("specialties"), cache.Removed);
    }
}
