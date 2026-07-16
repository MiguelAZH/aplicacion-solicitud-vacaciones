using System.Collections.Generic;

namespace SolicitudVacaciones.Web.Models;

public sealed class VacationDashboardViewModel
{
    public int AvailableDays { get; set; }
    public bool HasPendingRequest { get; set; }

    // Solicitudes del empleado actual (con filtro de estado)
    public IReadOnlyList<VacationRequest> Requests { get; set; } = [];

    // Bandejas de aprobación pendiente
    public IReadOnlyList<VacationRequest> PendingBossRequests { get; set; } = [];
    public IReadOnlyList<VacationRequest> PendingHRRequests { get; set; } = [];

    // Historial de solicitudes ya gestionadas
    public IReadOnlyList<VacationRequest> ReviewedBossRequests { get; set; } = [];
    public IReadOnlyList<VacationRequest> ReviewedHRRequests { get; set; } = [];

    // Empleados a cargo del jefe
    public IReadOnlyList<Employee> Subordinates { get; set; } = [];

    // Filtro de estado activo para la vista personal
    public string? StatusFilter { get; set; }

    // Filtro de historial para Jefe / RRHH (aprobadas, rechazadas, etc.)
    public string? ReviewFilter { get; set; }

    // Pestaña activa al cargar (historial, boss-inbox, team, hr-inbox)
    public string? ActiveTab { get; set; }
}
