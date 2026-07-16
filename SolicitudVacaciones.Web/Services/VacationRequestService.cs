using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SolicitudVacaciones.Web.Data;
using SolicitudVacaciones.Web.Models;

namespace SolicitudVacaciones.Web.Services;

public sealed class VacationRequestService : IVacationRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly IHolidayService _holidayService;

    public VacationRequestService(ApplicationDbContext context, IHolidayService holidayService)
    {
        _context = context;
        _holidayService = holidayService;
    }

    public IReadOnlyList<Employee> GetEmployees()
    {
        return _context.Employees.ToList();
    }

    public Employee? GetEmployeeById(int id)
    {
        return _context.Employees.Find(id);
    }

    public IReadOnlyList<VacationRequest> GetRequestsForEmployee(int employeeId)
    {
        return _context.VacationRequests
            .Where(r => r.EmployeeId == employeeId)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
    }

    public IReadOnlyList<VacationRequest> GetRequestsPendingBoss()
    {
        return _context.VacationRequests
            .Where(r => r.Status == VacationRequestStatus.PendienteJefe)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
    }

    public IReadOnlyList<VacationRequest> GetRequestsPendingHR()
    {
        return _context.VacationRequests
            .Where(r => r.Status == VacationRequestStatus.PendienteRRHH)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
    }

    // Solicitudes ya revisadas (aprobadas o rechazadas) por el Jefe
    public IReadOnlyList<VacationRequest> GetReviewedByBoss()
    {
        return _context.VacationRequests
            .Where(r => r.Status == VacationRequestStatus.PendienteRRHH
                     || r.Status == VacationRequestStatus.RechazadaJefe
                     || r.Status == VacationRequestStatus.ConfirmadaRRHH
                     || r.Status == VacationRequestStatus.RechazadaRRHH)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
    }

    // Solicitudes ya revisadas (confirmadas o rechazadas) por RRHH
    public IReadOnlyList<VacationRequest> GetReviewedByHR()
    {
        return _context.VacationRequests
            .Where(r => r.Status == VacationRequestStatus.ConfirmadaRRHH
                     || r.Status == VacationRequestStatus.RechazadaRRHH)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
    }

    // Subordinados del jefe indicado
    public IReadOnlyList<Employee> GetSubordinates(int bossId)
    {
        return _context.Employees
            .Where(e => e.Role == "Empleado" && e.BossId == bossId)
            .OrderBy(e => e.Name)
            .ToList();
    }

    // Ajustar días disponibles de un empleado
    public bool AdjustDays(int employeeId, int days, out string errorMessage)
    {
        if (days == 0)
        {
            errorMessage = "El ajuste debe ser distinto de cero.";
            return false;
        }
        var employee = _context.Employees.Find(employeeId);
        if (employee == null)
        {
            errorMessage = "Empleado no encontrado.";
            return false;
        }
        var newTotal = employee.AvailableDays + days;
        if (newTotal < 0)
        {
            errorMessage = $"No se pueden asignar {days} días: el empleado sólo tiene {employee.AvailableDays}.";
            return false;
        }
        employee.AvailableDays = newTotal;
        _context.Employees.Update(employee);
        _context.SaveChanges();
        errorMessage = string.Empty;
        return true;
    }

    public int GetAvailableDays(int employeeId)
    {
        var employee = _context.Employees.Find(employeeId);
        return employee?.AvailableDays ?? 0;
    }

    public bool HasPendingRequest(int employeeId)
    {
        return _context.VacationRequests.Any(r => r.EmployeeId == employeeId &&
            (r.Status == VacationRequestStatus.PendienteJefe || r.Status == VacationRequestStatus.PendienteRRHH));
    }

    public bool TryCreate(int employeeId, VacationRequestInputModel input, out string errorMessage)
    {
        // Validar: no fechas pasadas
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (input.StartDate < today)
        {
            errorMessage = "No se permiten solicitudes con fecha de inicio en el pasado.";
            return false;
        }

        // Validar: fecha inicio <= fecha fin
        if (input.EndDate < input.StartDate)
        {
            errorMessage = "La fecha final debe ser igual o posterior a la fecha de inicio.";
            return false;
        }

        // Validar: al menos 1 día hábil
        var workingDays = CountWeekdays(input.StartDate, input.EndDate);
        if (workingDays < 1)
        {
            errorMessage = "La solicitud debe contener al menos 1 día hábil. Revisa que el rango no caiga completamente en fines de semana o festivos.";
            return false;
        }

        if (HasPendingRequest(employeeId))
        {
            errorMessage = "Ya tienes una solicitud pendiente. Cancela o espera la decisión antes de crear otra.";
            return false;
        }

        var employee = _context.Employees.Find(employeeId);
        if (employee == null)
        {
            errorMessage = "Empleado no encontrado.";
            return false;
        }

        if (workingDays > employee.AvailableDays)
        {
            errorMessage = $"La solicitud requiere {workingDays} días hábiles y sólo tienes {employee.AvailableDays} días disponibles.";
            return false;
        }

        // Validar: no cruce con solicitudes activas del mismo empleado
        var overlaps = _context.VacationRequests.Any(r =>
            r.EmployeeId == employeeId &&
            r.Status != VacationRequestStatus.RechazadaJefe &&
            r.Status != VacationRequestStatus.RechazadaRRHH &&
            r.Status != VacationRequestStatus.Cancelada &&
            ((input.StartDate >= r.StartDate && input.StartDate <= r.EndDate) ||
             (input.EndDate >= r.StartDate && input.EndDate <= r.EndDate) ||
             (input.StartDate <= r.StartDate && input.EndDate >= r.EndDate)));

        if (overlaps)
        {
            errorMessage = "El periodo solicitado se cruza con otra solicitud ya registrada y activa.";
            return false;
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var request = new VacationRequest
            {
                EmployeeId = employeeId,
                StartDate = input.StartDate,
                EndDate = input.EndDate,
                WorkingDays = workingDays,
                Reason = input.Reason.Trim(),
                Status = VacationRequestStatus.PendienteJefe
            };
            _context.VacationRequests.Add(request);
            _context.SaveChanges();

            var audit = new AuditEntry
            {
                VacationRequestId = request.Id,
                ActorName = employee.Name,
                Action = "Creada",
                PreviousStatus = null,
                NewStatus = VacationRequestStatus.PendienteJefe,
                Comment = input.Reason.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditEntries.Add(audit);
            _context.SaveChanges();

            transaction.Commit();
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            errorMessage = "Error interno al crear la solicitud: " + ex.Message;
            return false;
        }
    }

    public bool Cancel(int id, int employeeId, out string errorMessage)
    {
        var request = _context.VacationRequests
            .Include(r => r.Employee)
            .FirstOrDefault(r => r.Id == id && r.EmployeeId == employeeId);

        if (request == null)
        {
            errorMessage = "Solicitud no encontrada.";
            return false;
        }
        if (request.Status != VacationRequestStatus.PendienteJefe)
        {
            errorMessage = "Sólo se pueden cancelar solicitudes en estado pendiente de aprobación por el jefe.";
            return false;
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var prevStatus = request.Status;
            request.Status = VacationRequestStatus.Cancelada;
            _context.VacationRequests.Update(request);

            var audit = new AuditEntry
            {
                VacationRequestId = request.Id,
                ActorName = request.Employee?.Name ?? "Empleado",
                Action = "Cancelada",
                PreviousStatus = prevStatus,
                NewStatus = VacationRequestStatus.Cancelada,
                Comment = "Cancelada por el empleado",
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditEntries.Add(audit);
            _context.SaveChanges();

            transaction.Commit();
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            errorMessage = "Error al cancelar la solicitud: " + ex.Message;
            return false;
        }
    }

    public bool ApproveByBoss(int id, string bossName, string comment, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            errorMessage = "El comentario de aprobación es obligatorio.";
            return false;
        }

        var request = _context.VacationRequests.Find(id);
        if (request == null)
        {
            errorMessage = "Solicitud no encontrada.";
            return false;
        }
        if (request.Status != VacationRequestStatus.PendienteJefe)
        {
            errorMessage = "La solicitud no está en estado pendiente de jefe.";
            return false;
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var prevStatus = request.Status;
            request.Status = VacationRequestStatus.PendienteRRHH;
            _context.VacationRequests.Update(request);

            var appComment = new ApprovalComment
            {
                VacationRequestId = id,
                AuthorName = bossName,
                Role = "Jefe",
                Text = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.ApprovalComments.Add(appComment);

            var audit = new AuditEntry
            {
                VacationRequestId = id,
                ActorName = bossName,
                Action = "Aprobada por Jefe",
                PreviousStatus = prevStatus,
                NewStatus = VacationRequestStatus.PendienteRRHH,
                Comment = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditEntries.Add(audit);
            _context.SaveChanges();

            transaction.Commit();
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            errorMessage = "Error al aprobar la solicitud: " + ex.Message;
            return false;
        }
    }

    public bool RejectByBoss(int id, string bossName, string comment, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            errorMessage = "El comentario de rechazo es obligatorio.";
            return false;
        }

        var request = _context.VacationRequests.Find(id);
        if (request == null)
        {
            errorMessage = "Solicitud no encontrada.";
            return false;
        }
        if (request.Status != VacationRequestStatus.PendienteJefe)
        {
            errorMessage = "La solicitud no está en estado pendiente de jefe.";
            return false;
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var prevStatus = request.Status;
            request.Status = VacationRequestStatus.RechazadaJefe;
            _context.VacationRequests.Update(request);

            var appComment = new ApprovalComment
            {
                VacationRequestId = id,
                AuthorName = bossName,
                Role = "Jefe",
                Text = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.ApprovalComments.Add(appComment);

            var audit = new AuditEntry
            {
                VacationRequestId = id,
                ActorName = bossName,
                Action = "Rechazada por Jefe",
                PreviousStatus = prevStatus,
                NewStatus = VacationRequestStatus.RechazadaJefe,
                Comment = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditEntries.Add(audit);
            _context.SaveChanges();

            transaction.Commit();
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            errorMessage = "Error al rechazar la solicitud: " + ex.Message;
            return false;
        }
    }

    public bool ConfirmByHR(int id, string hrName, string comment, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            errorMessage = "El comentario de confirmación es obligatorio.";
            return false;
        }

        var request = _context.VacationRequests
            .Include(r => r.Employee)
            .FirstOrDefault(r => r.Id == id);

        if (request == null)
        {
            errorMessage = "Solicitud no encontrada.";
            return false;
        }
        if (request.Status != VacationRequestStatus.PendienteRRHH)
        {
            errorMessage = "La solicitud no está en estado pendiente de RRHH.";
            return false;
        }

        var employee = request.Employee;
        if (employee == null)
        {
            errorMessage = "Empleado asociado no encontrado.";
            return false;
        }

        if (request.WorkingDays > employee.AvailableDays)
        {
            errorMessage = $"El empleado no tiene suficientes días disponibles ({employee.AvailableDays} días) para confirmar esta solicitud de {request.WorkingDays} días.";
            return false;
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var prevStatus = request.Status;

            // Descontar días
            employee.AvailableDays -= request.WorkingDays;
            _context.Employees.Update(employee);

            request.Status = VacationRequestStatus.ConfirmadaRRHH;
            _context.VacationRequests.Update(request);

            var appComment = new ApprovalComment
            {
                VacationRequestId = id,
                AuthorName = hrName,
                Role = "RRHH",
                Text = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.ApprovalComments.Add(appComment);

            var audit = new AuditEntry
            {
                VacationRequestId = id,
                ActorName = hrName,
                Action = "Confirmada por RRHH",
                PreviousStatus = prevStatus,
                NewStatus = VacationRequestStatus.ConfirmadaRRHH,
                Comment = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditEntries.Add(audit);
            _context.SaveChanges();

            transaction.Commit();
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            errorMessage = "Error al confirmar la solicitud: " + ex.Message;
            return false;
        }
    }

    public bool RejectByHR(int id, string hrName, string comment, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            errorMessage = "El comentario de rechazo es obligatorio.";
            return false;
        }

        var request = _context.VacationRequests.Find(id);
        if (request == null)
        {
            errorMessage = "Solicitud no encontrada.";
            return false;
        }
        if (request.Status != VacationRequestStatus.PendienteRRHH)
        {
            errorMessage = "La solicitud no está en estado pendiente de RRHH.";
            return false;
        }

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var prevStatus = request.Status;
            request.Status = VacationRequestStatus.RechazadaRRHH;
            _context.VacationRequests.Update(request);

            var appComment = new ApprovalComment
            {
                VacationRequestId = id,
                AuthorName = hrName,
                Role = "RRHH",
                Text = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.ApprovalComments.Add(appComment);

            var audit = new AuditEntry
            {
                VacationRequestId = id,
                ActorName = hrName,
                Action = "Rechazada por RRHH",
                PreviousStatus = prevStatus,
                NewStatus = VacationRequestStatus.RechazadaRRHH,
                Comment = comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditEntries.Add(audit);
            _context.SaveChanges();

            transaction.Commit();
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            errorMessage = "Error al rechazar la solicitud: " + ex.Message;
            return false;
        }
    }

    public VacationRequest? GetRequestDetails(int id)
    {
        return _context.VacationRequests
            .Include(r => r.Employee)
            .Include(r => r.Comments)
            .Include(r => r.Audits)
            .FirstOrDefault(r => r.Id == id);
    }

    private int CountWeekdays(DateOnly start, DateOnly end)
    {
        var days = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                var holidays = _holidayService.GetHolidays(date.Year);
                if (!holidays.Contains(date))
                    days++;
            }
        }
        return days;
    }
}
