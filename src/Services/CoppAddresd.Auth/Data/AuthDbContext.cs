using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Data;

public class AuthDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options) { }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<ApplicationUserRole> ApplicationUserRole => Set<ApplicationUserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<UserApplication> UserApplications => Set<UserApplication>();
    public DbSet<ErpAccessOperation> ErpAccessOperations => Set<ErpAccessOperation>();
    public DbSet<ScopedRoleAssignment> ScopedRoleAssignments => Set<ScopedRoleAssignment>();
    public DbSet<ScopedPermissionAssignment> ScopedPermissionAssignments =>
        Set<ScopedPermissionAssignment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserApplication>().Property(x => x.IsSuspended).HasDefaultValue(false);
        builder.Entity<UserApplication>().Property(x => x.SessionVersion).HasDefaultValue(0L).IsConcurrencyToken();
        builder.Entity<RefreshToken>().Property(x => x.ApplicationSessionVersion).HasDefaultValue(0L);
        builder.Entity<ErpAccessOperation>(b =>
        {
            b.ToTable("ErpAccessOperations", "auth");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasMaxLength(16).IsRequired();
            b.HasIndex(x => x.UserId).IsUnique().HasFilter("\"CompletedAt\" IS NULL");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users", "auth");
            b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("Roles", "auth");
            b.Property(r => r.Name).HasMaxLength(100).IsRequired();
            b.Property(r => r.Description).HasMaxLength(500);
            b.Property(r => r.IsSystem).HasDefaultValue(false);
        });

        builder.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable("UserClaims", "auth");
        });

        builder.Entity<IdentityUserRole<Guid>>(b =>
        {
            b.ToTable("UserRoles", "auth");
        });

        builder.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable("UserLogins", "auth");
        });

        builder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("UserTokens", "auth");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(b =>
        {
            b.ToTable("RoleClaims", "auth");
        });

        builder.Entity<ApplicationUserRole>(b =>
        {
            b.ToTable("UserRoleAssignments", "auth");
            b.HasKey(ur => new { ur.UserId, ur.RoleId });

            b.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Permission>(b =>
        {
            b.ToTable("Permissions", "auth");
            b.HasKey(p => p.Id);
            b.Property(p => p.Code).HasMaxLength(100).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(500);
            b.Property(p => p.Module).HasMaxLength(100).IsRequired();
            b.HasIndex(p => p.Code).IsUnique();
            b.HasIndex(p => p.Module);
        });

        builder.Entity<RolePermission>(b =>
        {
            b.ToTable("RolePermissions", "auth");
            b.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            b.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions", "auth");
            b.HasKey(up => new { up.UserId, up.PermissionId });

            b.HasOne(up => up.User)
                .WithMany(u => u.UserPermissions)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(up => up.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(up => up.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens", "auth");
            b.HasKey(rt => rt.Id);
            b.Property(rt => rt.Token).HasMaxLength(256).IsRequired();
            b.HasIndex(rt => rt.Token).IsUnique();
            b.HasIndex(rt => rt.UserId);

            b.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(rt => rt.ReplacedByToken)
                .WithMany()
                .HasForeignKey(rt => rt.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(rt => rt.Application)
                .WithMany()
                .HasForeignKey(rt => rt.ApplicationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Application>(b =>
        {
            b.ToTable("Applications", "auth");
            b.HasKey(a => a.Id);
            b.Property(a => a.Code).HasMaxLength(50).IsRequired();
            b.Property(a => a.Name).HasMaxLength(100).IsRequired();
            b.Property(a => a.Description).HasMaxLength(500);
            b.Property(a => a.IsActive).IsRequired();
            b.HasIndex(a => a.Code).IsUnique();
        });

        builder.Entity<UserApplication>(b =>
        {
            b.ToTable("UserApplications", "auth");
            b.HasKey(ua => new { ua.UserId, ua.ApplicationId });

            b.HasOne(ua => ua.User)
                .WithMany(u => u.UserApplications)
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(ua => ua.Application)
                .WithMany(a => a.UserApplications)
                .HasForeignKey(ua => ua.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ScopedRoleAssignment>(b =>
        {
            b.ToTable("ScopedRoleAssignments", "auth");
            b.HasKey(s => s.Id);
            b.Property(s => s.ScopeType).HasMaxLength(20).IsRequired();
            b.HasIndex(s => s.UserId);

            b.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(s => s.Role)
                .WithMany()
                .HasForeignKey(s => s.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ScopedPermissionAssignment>(b =>
        {
            b.ToTable("ScopedPermissionAssignments", "auth");
            b.HasKey(s => s.Id);
            b.Property(s => s.ScopeType).HasMaxLength(20).IsRequired();
            b.Property(s => s.Effect).HasMaxLength(10).IsRequired();
            b.HasIndex(s => s.UserId);
            b.HasIndex(s => new
                {
                    s.UserId,
                    s.ScopeType,
                    s.ScopeId,
                    s.PermissionId,
                })
                .HasDatabaseName("ix_scoped_permission_assignments_scope_lookup");

            b.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(s => s.Permission)
                .WithMany()
                .HasForeignKey(s => s.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invitation>(b =>
        {
            b.ToTable("Invitations", "auth");
            b.HasKey(i => i.Id);
            b.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
            b.Property(i => i.ExpiresAt).IsRequired();
            b.HasIndex(i => i.TokenHash).IsUnique();
            b.HasIndex(i => i.UserId);

            b.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OtpCode>(b =>
        {
            b.ToTable("OtpCodes", "auth");
            b.HasKey(o => o.Id);
            b.Property(o => o.DocumentNumber).HasMaxLength(50).IsRequired();
            b.Property(o => o.Channel).HasMaxLength(20).IsRequired();
            b.Property(o => o.Target).HasMaxLength(320).IsRequired();
            b.Property(o => o.CodeHash).HasMaxLength(128).IsRequired();
            b.Property(o => o.Salt).HasMaxLength(128).IsRequired();
            b.HasIndex(o => o.DocumentNumber);
        });

        builder.Entity<UserPreference>(b =>
        {
            b.ToTable("UserPreferences", "auth");
            b.HasKey(u => u.UserId);
            b.Property(u => u.Lang).HasMaxLength(2);
            b.Property(u => u.AccentColor).HasMaxLength(16);
            b.Property(u => u.AvatarConfiguration).HasColumnType("jsonb");
            b.HasOne(u => u.User)
                .WithMany()
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
