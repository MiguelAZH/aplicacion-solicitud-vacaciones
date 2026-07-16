using System;

namespace SolicitudVacaciones.Web.Models;

public sealed class AuditEntry
{
    public int Id { get; set; }
    public int VacationRequestId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Creada", "Aprobada por Jefe", "Rechazada por Jefe", "Confirmada por RRHH", "Rechazada por RRHH", "Cancelada"
    public VacationRequestStatus? PreviousStatus { get; set; }
    public VacationRequestStatus NewStatus { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Propiedad de navegación
    public VacationRequest? VacationRequest { get; set; }
}
