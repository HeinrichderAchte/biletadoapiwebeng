using Microsoft.EntityFrameworkCore;
using Biletado.Models;

namespace Biletado.Persistence.Contexts;

public class ReservationsDbContext : DbContext
{
    public ReservationsDbContext(DbContextOptions<ReservationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Reservation> Reservations { get; set; } = null!;
    public DbSet<Room> Rooms { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        
        modelBuilder.Entity<Reservation>(r =>
        {
            r.ToTable("reservations");
            r.HasKey(x => x.reservationId);
            r.Property(x => x.reservationId).HasColumnName("id");
            r.Property(x => x.fromDate).HasColumnName("from").IsRequired();
            r.Property(x => x.toDate).HasColumnName("to");
            r.Property(x => x.roomId).HasColumnName("room_id");
            r.Property(x => x.deletedAt).HasColumnName("deleted_at");
        });
        
        modelBuilder.Entity<Room>(r =>
        {
            r.ToTable("rooms");
            r.HasKey(x => x.roomId); 
            r.Property(x => x.roomId).HasColumnName("id");
        });

    }
}
