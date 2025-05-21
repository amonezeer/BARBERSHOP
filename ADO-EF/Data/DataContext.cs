using ADO_EF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADO_EF.Data
{
    public class DataContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<UserAccess> UserAccesses { get; set; }
        public DbSet<VerificationCode> VerificationCodes { get; set; }
        public DbSet<Barber> Barbers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasKey(u => u.Id);
            modelBuilder.Entity<UserAccess>().HasKey(ua => ua.Id);
            modelBuilder.Entity<VerificationCode>().HasKey(vc => vc.Id);
            modelBuilder.Entity<Barber>().HasKey(b => b.Id);
            modelBuilder.Entity<Service>().HasKey(s => s.Id);
            modelBuilder.Entity<Booking>().HasKey(b => b.Id);

            modelBuilder.Entity<UserAccess>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(ua => ua.UserId);

            modelBuilder.Entity<VerificationCode>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(vc => vc.UserId);

            modelBuilder.Entity<Booking>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(b => b.UserId);

            modelBuilder.Entity<Booking>()
                .HasOne<Barber>()
                .WithMany()
                .HasForeignKey(b => b.BarberId);

            modelBuilder.Entity<Booking>()
                .HasOne<Service>()
                .WithMany()
                .HasForeignKey(b => b.ServiceId);

            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasColumnType("decimal(10, 2)");
        }
    }
}