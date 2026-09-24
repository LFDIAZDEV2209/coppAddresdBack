using CoppAddresd.Domain.Entities.Redes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.Redes;

/// <summary>Radicación de autorización de red en el schema <c>erp</c>.</summary>
public sealed class RadicacionRedConfiguration : IEntityTypeConfiguration<RadicacionRed>
{
    public void Configure(EntityTypeBuilder<RadicacionRed> builder)
    {
        builder.ToTable("radicaciones", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Consecutivo)
            .HasColumnName("consecutivo")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Nit).HasColumnName("nit").HasMaxLength(20).IsRequired();
        builder.Property(x => x.RazonSocial).HasColumnName("razon_social").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Nivel).HasColumnName("nivel").HasMaxLength(20).IsRequired();
        builder.Property(x => x.DiagnosticoCie10).HasColumnName("diagnostico_cie10").HasMaxLength(10).IsRequired();
        builder.Property(x => x.DiagnosticoDescripcion).HasColumnName("diagnostico_descripcion").HasMaxLength(300);
        builder.Property(x => x.Prioridad).HasColumnName("prioridad").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(1000);
        builder.Property(x => x.CotizacionNombreArchivo).HasColumnName("cotizacion_nombre_archivo").HasMaxLength(200);
        builder.Property(x => x.CotizacionNumero).HasColumnName("cotizacion_numero").HasMaxLength(50);
        builder.Property(x => x.CotizacionFecha).HasColumnName("cotizacion_fecha").HasColumnType("date");
        builder.Property(x => x.CotizacionMonto).HasColumnName("cotizacion_monto").HasColumnType("numeric(18,2)");
        builder.Property(x => x.Total).HasColumnName("total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(15).IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Consecutivo)
            .HasDatabaseName("ix_radicaciones_consecutivo")
            .IsUnique();
        builder.HasIndex(x => x.Nit).HasDatabaseName("ix_radicaciones_nit");
        builder.HasIndex(x => x.Estado).HasDatabaseName("ix_radicaciones_estado");

        // Líneas: agregado de la radicación, borrado en cascada.
        builder.HasMany(x => x.Lineas)
            .WithOne()
            .HasForeignKey(l => l.RadicacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Línea CUPS/CUM de la radicación en el schema <c>erp</c>.</summary>
public sealed class RadicacionRedLineaConfiguration : IEntityTypeConfiguration<RadicacionRedLinea>
{
    public void Configure(EntityTypeBuilder<RadicacionRedLinea> builder)
    {
        builder.ToTable("radicacion_cups", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RadicacionId).HasColumnName("radicacion_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(5).IsRequired();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(300);
        builder.Property(x => x.Cantidad).HasColumnName("cantidad").IsRequired();
        builder.Property(x => x.ValorUnitario).HasColumnName("valor_unitario").HasColumnType("numeric(18,2)").IsRequired();

        builder.HasIndex(x => x.RadicacionId).HasDatabaseName("ix_radicacion_cups_radicacion_id");
    }
}
