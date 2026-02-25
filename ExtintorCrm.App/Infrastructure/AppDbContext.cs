using System;
using ExtintorCrm.App.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
        public DbSet<ConfiguracaoAlerta> ConfiguracoesAlerta => Set<ConfiguracaoAlerta>();
        public DbSet<DocumentoAnexo> DocumentosAnexo => Set<DocumentoAnexo>();

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }

            var dbPath = GetDatabasePath();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.NomeFantasia).IsRequired();
                entity.Property(x => x.CPF).HasMaxLength(18);
                entity.HasIndex(x => x.CPF).IsUnique();
            });

            modelBuilder.Entity<Pagamento>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.CpfCnpjCliente).HasMaxLength(18);
                entity.Property(x => x.Valor).HasColumnType("decimal(18,2)");
                entity.Property(x => x.ValorPrevisto).HasColumnType("decimal(18,2)");
                entity.Property(x => x.ValorEfetivo).HasColumnType("decimal(18,2)");
                entity.HasIndex(x => x.CpfCnpjCliente);
                entity.HasOne<Cliente>()
                    .WithMany()
                    .HasPrincipalKey(x => x.CPF)
                    .HasForeignKey(x => x.CpfCnpjCliente)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ConfiguracaoAlerta>(entity =>
            {
                entity.HasKey(x => x.Id);
            });

            modelBuilder.Entity<DocumentoAnexo>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Contexto).IsRequired().HasMaxLength(32);
                entity.Property(x => x.TipoDocumento).IsRequired().HasMaxLength(64);
                entity.Property(x => x.NomeOriginal).IsRequired().HasMaxLength(260);
                entity.Property(x => x.CaminhoRelativo).IsRequired().HasMaxLength(1024);
                entity.HasIndex(x => x.PagamentoId);
                entity.HasIndex(x => x.ClienteId);

                entity.HasOne<Pagamento>()
                    .WithMany()
                    .HasForeignKey(x => x.PagamentoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Cliente>()
                    .WithMany()
                    .HasForeignKey(x => x.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        public static string GetDatabasePath()
        {
            AppDataPaths.EnsureInitialized();
            return AppDataPaths.DatabasePath;
        }
    }
}
