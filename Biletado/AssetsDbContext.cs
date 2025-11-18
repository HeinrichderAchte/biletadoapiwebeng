using Biletado.Models;
using Microsoft.EntityFrameworkCore;

namespace Biletado;

public class AssetsDbContext(DbContextOptions<AssetsDbContext> options) : DbContext(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Storey> Stores => Set<Storey>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        // Building mapping
        modelBuilder.Entity<Building>(b =>
        {
            b.ToTable("buildings");
            b.HasKey(x => x.buildingId);
            b.Property(x => x.buildingId).HasColumnName("id");
            b.Property(x => x.name).HasColumnName("name").IsRequired();
            b.Property(x => x.countryCode).HasColumnName("country_code");
            b.Property(x => x.streetName).HasColumnName("streetname");
            b.Property(x => x.houseNumber).HasColumnName("housenumber");
            b.Property(x => x.city).HasColumnName("city");
            b.Property(x => x.postalCode).HasColumnName("postalcode");
            b.Property(x => x.deletedAt).HasColumnName("deleted_at");
        });

        // Storey mapping
        modelBuilder.Entity<Storey>(s =>
        {
            s.ToTable("storeys");
            s.HasKey(x => x.storeyId);
            s.Property(x => x.storeyId).HasColumnName("id");
            s.Property(x => x.storeyName).HasColumnName("name").IsRequired();
            s.Property(x => x.buildingId).HasColumnName("building_id");
            s.Property(x => x.deletedAt).HasColumnName("deleted_at");
        });

        // Room mapping
        modelBuilder.Entity<Room>(r =>
        {
            r.ToTable("rooms");
            r.HasKey(x => x.roomId);
            r.Property(x => x.roomId).HasColumnName("id");
            r.Property(x => x.roomName).HasColumnName("name").IsRequired();
            r.Property(x => x.storeyId).HasColumnName("storey_id");
            r.Property(x => x.deletedAt).HasColumnName("deleted_at");
        });
    }    
}

