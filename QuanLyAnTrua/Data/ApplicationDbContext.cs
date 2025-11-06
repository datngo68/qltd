using Microsoft.EntityFrameworkCore;
using QuanLyAnTrua.Models;

namespace QuanLyAnTrua.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseParticipant> ExpenseParticipants { get; set; }
        public DbSet<MonthlyPayment> MonthlyPayments { get; set; }
        public DbSet<SharedReport> SharedReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ExpenseParticipant composite key
            modelBuilder.Entity<ExpenseParticipant>()
                .HasKey(ep => new { ep.ExpenseId, ep.UserId });

            // Configure relationships
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Payer)
                .WithMany(u => u.ExpensesAsPayer)
                .HasForeignKey(e => e.PayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExpenseParticipant>()
                .HasOne(ep => ep.Expense)
                .WithMany(e => e.Participants)
                .HasForeignKey(ep => ep.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExpenseParticipant>()
                .HasOne(ep => ep.User)
                .WithMany(u => u.ExpenseParticipants)
                .HasForeignKey(ep => ep.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MonthlyPayment>()
                .HasOne(mp => mp.User)
                .WithMany(u => u.MonthlyPayments)
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MonthlyPayment>()
                .HasOne(mp => mp.Creditor)
                .WithMany()
                .HasForeignKey(mp => mp.CreditorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure unique monthly payment per user per month
            modelBuilder.Entity<MonthlyPayment>()
                .HasIndex(mp => new { mp.UserId, mp.Year, mp.Month })
                .IsUnique(false);

            // Configure Group relationships
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Creator)
                .WithMany()
                .HasForeignKey(g => g.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Group)
                .WithMany(g => g.Users)
                .HasForeignKey(u => u.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Group)
                .WithMany(g => g.Expenses)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MonthlyPayment>()
                .HasOne(mp => mp.Group)
                .WithMany(g => g.MonthlyPayments)
                .HasForeignKey(mp => mp.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SharedReport relationships
            modelBuilder.Entity<SharedReport>()
                .HasOne(sr => sr.User)
                .WithMany()
                .HasForeignKey(sr => sr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SharedReport>()
                .HasOne(sr => sr.Group)
                .WithMany()
                .HasForeignKey(sr => sr.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SharedReport>()
                .HasOne(sr => sr.Creator)
                .WithMany()
                .HasForeignKey(sr => sr.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure unique token
            modelBuilder.Entity<SharedReport>()
                .HasIndex(sr => sr.Token)
                .IsUnique();
        }
    }
}

