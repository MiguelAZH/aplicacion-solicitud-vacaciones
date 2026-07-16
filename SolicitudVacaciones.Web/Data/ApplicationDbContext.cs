using Microsoft.EntityFrameworkCore;
using SolicitudVacaciones.Web.Models;

namespace SolicitudVacaciones.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<VacationRequest> VacationRequests { get; set; } = null!;
        public DbSet<ApprovalComment> ApprovalComments { get; set; } = null!;
        public DbSet<AuditEntry> AuditEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relaciones de VacationRequest
            modelBuilder.Entity<VacationRequest>()
                .HasOne(vr => vr.Employee)
                .WithMany()
                .HasForeignKey(vr => vr.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relaciones de ApprovalComment
            modelBuilder.Entity<ApprovalComment>()
                .HasOne(ac => ac.VacationRequest)
                .WithMany(vr => vr.Comments)
                .HasForeignKey(ac => ac.VacationRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relaciones de AuditEntry
            modelBuilder.Entity<AuditEntry>()
                .HasOne(ae => ae.VacationRequest)
                .WithMany(vr => vr.Audits)
                .HasForeignKey(ae => ae.VacationRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Semilla de Empleados
            modelBuilder.Entity<Employee>().HasData(
                new Employee { Id = 1, Name = "Juan Pérez", Role = "Empleado", AvailableDays = 15, BossId = 2 },
                new Employee { Id = 2, Name = "María Gómez", Role = "Jefe", AvailableDays = 15 },
                new Employee { Id = 3, Name = "Carlos Ruiz", Role = "RRHH", AvailableDays = 15 },
                new Employee { Id = 4, Name = "Ana López", Role = "Empleado", AvailableDays = 12, BossId = 2 }
            );
        }
    }
}
