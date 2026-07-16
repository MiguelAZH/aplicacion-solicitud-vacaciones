namespace SolicitudVacaciones.Web.Models;

public sealed class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Empleado", "Jefe", "RRHH"
    public int AvailableDays { get; set; } = 15;
    public int? BossId { get; set; }
}
