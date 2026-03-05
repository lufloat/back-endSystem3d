using Microsoft.EntityFrameworkCore;
using SistemaProducao3D.Modelos.Modelos;
using System;
using System.Linq;

namespace SistemaProducao3D.Data.Context
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        public DbSet<MesaProducao> MesasProducao { get; set; }
        public DbSet<Material> Materiais { get; set; }
        public DbSet<EventoImpressora> EventosImpressora { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===================================
            // MESAS DE PRODUÇÃO
            // ===================================
            modelBuilder.Entity<MesaProducao>(entity =>
            {
                entity.ToTable("mesas_producao");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DatetimeStarted).HasColumnName("datetime_started").IsRequired();
                entity.Property(e => e.DatetimeFinished).HasColumnName("datetime_finished");
                entity.Property(e => e.Material0Amount).HasColumnName("material_0_amount");
                entity.Property(e => e.Material1Amount).HasColumnName("material_1_amount");
                entity.Property(e => e.Material0Guid).HasColumnName("material_0_guid");
                entity.Property(e => e.Material1Guid).HasColumnName("material_1_guid");
                entity.Property(e => e.Material0WeightG).HasColumnName("material_0_weight_g");
                entity.Property(e => e.Material1WeightG).HasColumnName("material_1_weight_g");
                entity.Property(e => e.PrintTime).HasColumnName("print_time");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.MesaId).HasColumnName("mesa_id");
                entity.Property(e => e.MachineId).HasColumnName("machine_id");
                entity.Property(e => e.JobName).HasColumnName("job_name");
                entity.Property(e => e.JobId).HasColumnName("job_id");
                entity.Property(e => e.UltimakerJobUuid).HasColumnName("ultimaker_job_uuid");
                entity.Property(e => e.IsPrototype).HasColumnName("is_prototype");
                entity.Property(e => e.IsRecondicionado).HasColumnName("is_recondicionado");
                entity.Property(e => e.JobType).HasColumnName("job_type");
            });

            // ===================================
            // MATERIAIS
            // ===================================
            modelBuilder.Entity<Material>(entity =>
            {
                entity.ToTable("materiais");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UltimakerMaterialGuid)
                    .HasColumnName("ultimaker_material_guid")
                    .IsRequired();

                entity.HasIndex(e => e.UltimakerMaterialGuid).IsUnique();

                entity.Property(e => e.Nome)
                    .HasColumnName("nome")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.Densidade)
                    .HasColumnName("densidade")
                    .HasPrecision(5, 3)
                    .IsRequired();

                entity.Property(e => e.Fabricante)
                    .HasColumnName("fabricante")
                    .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ===================================
            // EVENTOS IMPRESSORA
            // ===================================
            modelBuilder.Entity<EventoImpressora>(entity =>
            {
                entity.ToTable("eventos_impressora");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.MachineId).HasColumnName("machine_id").IsRequired();
                entity.Property(e => e.JobUuid).HasColumnName("job_uuid").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Time).HasColumnName("time").IsRequired();
                entity.Property(e => e.TypeId).HasColumnName("type_id").IsRequired();
                entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(500);

                // Índice para buscas por período e impressora
                entity.HasIndex(e => new { e.MachineId, e.Time });
                entity.HasIndex(e => e.JobUuid);

                // Evita duplicatas
                entity.HasIndex(e => new { e.MachineId, e.JobUuid, e.TypeId, e.Time }).IsUnique();
            });
        }

        public override int SaveChanges()
        {
            ConvertDatesToUtc();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ConvertDatesToUtc();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ConvertDatesToUtc()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.CurrentValue is DateTime dateTime)
                    {
                        if (dateTime.Kind == DateTimeKind.Unspecified)
                            prop.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                        else if (dateTime.Kind == DateTimeKind.Local)
                            prop.CurrentValue = dateTime.ToUniversalTime();
                    }
                }
            }
        }
    }
}