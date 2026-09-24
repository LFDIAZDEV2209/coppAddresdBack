using CoppAddresd.Domain.Entities.Redes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.Redes;

/// <summary>Factura RIPS de red en el schema <c>erp</c>.</summary>
public sealed class FacturaRedConfiguration : IEntityTypeConfiguration<FacturaRed>
{
    public void Configure(EntityTypeBuilder<FacturaRed> builder)
    {
        builder.ToTable("facturas_rips", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Cuv).HasColumnName("cuv").HasMaxLength(50).IsRequired();
        builder.Property(x => x.FacturaNumero).HasColumnName("factura_numero").HasMaxLength(50).IsRequired();
        builder.Property(x => x.PrestadorNit).HasColumnName("prestador_nit").HasMaxLength(20).IsRequired();
        builder.Property(x => x.PrestadorRazonSocial).HasColumnName("prestador_razon_social").HasMaxLength(200).IsRequired();
        builder.Property(x => x.FechaRadicacion).HasColumnName("fecha_radicacion").HasColumnType("date").IsRequired();
        builder.Property(x => x.ValorTotal).HasColumnName("valor_total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.ValorCopago).HasColumnName("valor_copago").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.ValorCuotaModeradora).HasColumnName("valor_cuota_moderadora").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.ValorNeto).HasColumnName("valor_neto").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.UsuarioTipo).HasColumnName("usuario_tipo").HasMaxLength(15).IsRequired();
        builder.Property(x => x.UsuarioDocumento).HasColumnName("usuario_documento").HasMaxLength(30);
        builder.Property(x => x.UsuarioNombre).HasColumnName("usuario_nombre").HasMaxLength(200);
        builder.Property(x => x.NumeroContrato).HasColumnName("numero_contrato").HasMaxLength(50);
        builder.Property(x => x.ModalidadContrato).HasColumnName("modalidad_contrato").HasMaxLength(30);
        builder.Property(x => x.Cobertura).HasColumnName("cobertura").HasMaxLength(100);
        builder.Property(x => x.PeriodoAtencion).HasColumnName("periodo_atencion").HasMaxLength(50);
        builder.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(15).IsRequired();
        builder.Property(x => x.RegistrosJson)
            .HasColumnName("registros")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Cuv)
            .HasDatabaseName("ix_facturas_rips_cuv")
            .IsUnique();
        builder.HasIndex(x => x.PrestadorNit).HasDatabaseName("ix_facturas_rips_prestador_nit");
        builder.HasIndex(x => x.Estado).HasDatabaseName("ix_facturas_rips_estado");

        builder.HasMany(x => x.Archivos)
            .WithOne()
            .HasForeignKey(a => a.FacturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Archivo RIPS de la factura en el schema <c>erp</c>.</summary>
public sealed class FacturaRedArchivoConfiguration : IEntityTypeConfiguration<FacturaRedArchivo>
{
    public void Configure(EntityTypeBuilder<FacturaRedArchivo> builder)
    {
        builder.ToTable("facturas_rips_archivos", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.FacturaId).HasColumnName("factura_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(5).IsRequired();
        builder.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Registros).HasColumnName("registros").IsRequired();
        builder.Property(x => x.Tamano).HasColumnName("tamano").HasMaxLength(20);

        builder.HasIndex(x => x.FacturaId).HasDatabaseName("ix_facturas_rips_archivos_factura_id");
    }
}
