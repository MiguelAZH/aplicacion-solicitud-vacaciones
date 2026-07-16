using System;
using System.Collections.Generic;

namespace SolicitudVacaciones.Web.Models;

public enum VacationRequestStatus
{
    PendienteJefe,
    RechazadaJefe,
    PendienteRRHH,
    ConfirmadaRRHH,
    RechazadaRRHH,
    Cancelada
}

public sealed class VacationRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int WorkingDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public VacationRequestStatus Status { get; set; }

    public string StatusDisplay => Status switch
    {
        VacationRequestStatus.PendienteJefe => "Pendiente Aprobación Jefe",
        VacationRequestStatus.RechazadaJefe => "Rechazada por Jefe",
        VacationRequestStatus.PendienteRRHH => "Pendiente Confirmación RRHH",
        VacationRequestStatus.ConfirmadaRRHH => "Confirmada",
        VacationRequestStatus.RechazadaRRHH => "Rechazada por RRHH",
        VacationRequestStatus.Cancelada => "Cancelada",
        _ => "Desconocido"
    };

    /// <summary>Decisión del jefe sobre esta solicitud (para historial del jefe).</summary>
    public string BossDecisionDisplay => Status switch
    {
        VacationRequestStatus.RechazadaJefe => "Rechazada",
        VacationRequestStatus.PendienteRRHH => "Aprobada",
        VacationRequestStatus.ConfirmadaRRHH => "Aprobada",
        VacationRequestStatus.RechazadaRRHH => "Aprobada",
        _ => "—"
    };

    /// <summary>Decisión de RRHH sobre esta solicitud (para historial de RRHH).</summary>
    public string HRDecisionDisplay => Status switch
    {
        VacationRequestStatus.ConfirmadaRRHH => "Confirmada",
        VacationRequestStatus.RechazadaRRHH => "Rechazada",
        _ => "—"
    };

    // Propiedades de navegación
    public Employee? Employee { get; set; }
    public List<ApprovalComment> Comments { get; set; } = [];
    public List<AuditEntry> Audits { get; set; } = [];
}
