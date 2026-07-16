using System;

namespace SolicitudVacaciones.Web.Models;

public sealed class ApprovalComment
{
    public int Id { get; set; }
    public int VacationRequestId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Jefe" o "RRHH"
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Propiedad de navegación
    public VacationRequest? VacationRequest { get; set; }
}
