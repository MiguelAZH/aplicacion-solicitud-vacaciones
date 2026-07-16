using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SolicitudVacaciones.Web.Models;
using SolicitudVacaciones.Web.Services;

namespace SolicitudVacaciones.Web.Controllers;

public class HomeController : Controller
{
    private readonly IVacationRequestService _vacationRequests;
    private readonly IHolidayService _holidayService;

    public HomeController(IVacationRequestService vacationRequests, IHolidayService holidayService)
    {
        _vacationRequests = vacationRequests;
        _holidayService = holidayService;
    }

    private Employee GetCurrentUser()
    {
        if (Request.Cookies.TryGetValue("SimulatedUserId", out var cookieValue) && int.TryParse(cookieValue, out var userId))
        {
            var emp = _vacationRequests.GetEmployeeById(userId);
            if (emp != null) return emp;
        }
        var defaultEmp = _vacationRequests.GetEmployees().FirstOrDefault();
        return defaultEmp ?? new Employee { Id = 1, Name = "Juan Pérez", Role = "Empleado", AvailableDays = 15 };
    }

    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        ViewBag.CurrentUser = GetCurrentUser();
        ViewBag.AllEmployees = _vacationRequests.GetEmployees();
        base.OnActionExecuting(context);
    }

    [HttpPost]
    public IActionResult ChangeUser(int userId)
    {
        Response.Cookies.Append("SimulatedUserId", userId.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer))
        {
            return Redirect(referer);
        }
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Index(string? statusFilter = null, string? reviewFilter = null, string? tab = null)
    {
        var currentUser = GetCurrentUser();
        var allRequests = _vacationRequests.GetRequestsForEmployee(currentUser.Id);

        // Aplicar filtro si se solicita (vista empleado)
        var filteredRequests = statusFilter switch
        {
            "PendienteJefe" => allRequests.Where(r => r.Status == VacationRequestStatus.PendienteJefe).ToList(),
            "PendienteRRHH" => allRequests.Where(r => r.Status == VacationRequestStatus.PendienteRRHH).ToList(),
            "ConfirmadaRRHH" => allRequests.Where(r => r.Status == VacationRequestStatus.ConfirmadaRRHH).ToList(),
            "RechazadaJefe" => allRequests.Where(r => r.Status == VacationRequestStatus.RechazadaJefe).ToList(),
            "RechazadaRRHH" => allRequests.Where(r => r.Status == VacationRequestStatus.RechazadaRRHH).ToList(),
            "Cancelada" => allRequests.Where(r => r.Status == VacationRequestStatus.Cancelada).ToList(),
            _ => allRequests.ToList()
        };

        var reviewedBoss = _vacationRequests.GetReviewedByBoss();
        var reviewedHR = _vacationRequests.GetReviewedByHR();

        if (currentUser.Role == "Jefe" && !string.IsNullOrEmpty(reviewFilter))
        {
            reviewedBoss = reviewFilter switch
            {
                "aprobadas" => reviewedBoss.Where(r => r.Status != VacationRequestStatus.RechazadaJefe).ToList(),
                "rechazadas" => reviewedBoss.Where(r => r.Status == VacationRequestStatus.RechazadaJefe).ToList(),
                _ => reviewedBoss
            };
        }
        else if (currentUser.Role == "RRHH" && !string.IsNullOrEmpty(reviewFilter))
        {
            reviewedHR = reviewFilter switch
            {
                "confirmadas" => reviewedHR.Where(r => r.Status == VacationRequestStatus.ConfirmadaRRHH).ToList(),
                "rechazadas" => reviewedHR.Where(r => r.Status == VacationRequestStatus.RechazadaRRHH).ToList(),
                _ => reviewedHR
            };
        }

        return View(new VacationDashboardViewModel
        {
            AvailableDays = _vacationRequests.GetAvailableDays(currentUser.Id),
            HasPendingRequest = _vacationRequests.HasPendingRequest(currentUser.Id),
            Requests = filteredRequests,
            PendingBossRequests = _vacationRequests.GetRequestsPendingBoss(),
            PendingHRRequests = _vacationRequests.GetRequestsPendingHR(),
            ReviewedBossRequests = reviewedBoss,
            ReviewedHRRequests = reviewedHR,
            Subordinates = currentUser.Role == "Jefe"
                ? _vacationRequests.GetSubordinates(currentUser.Id)
                : [],
            StatusFilter = statusFilter,
            ReviewFilter = reviewFilter,
            ActiveTab = tab
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role is "Jefe" or "RRHH")
        {
            TempData["Error"] = "Los roles de Jefe y RRHH no tienen permitido crear solicitudes de vacaciones.";
            return RedirectToAction(nameof(Index));
        }

        if (_vacationRequests.HasPendingRequest(currentUser.Id))
        {
            TempData["Error"] = "Ya tienes una solicitud pendiente. No puedes crear otra hasta que se resuelva o la canceles.";
            return RedirectToAction(nameof(Index));
        }

        return View(new VacationRequestInputModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(11))
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(VacationRequestInputModel input)
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role is "Jefe" or "RRHH")
        {
            TempData["Error"] = "Los roles de Jefe y RRHH no tienen permitido crear solicitudes de vacaciones.";
            return RedirectToAction(nameof(Index));
        }

        if (input.EndDate < input.StartDate)
            ModelState.AddModelError(nameof(input.EndDate), "La fecha final debe ser posterior o igual a la fecha inicial.");

        if (!ModelState.IsValid)
            return View(input);

        if (!_vacationRequests.TryCreate(currentUser.Id, input, out var errorMessage))
        {
            ModelState.AddModelError(string.Empty, errorMessage);
            return View(input);
        }

        TempData["Success"] = "Tu solicitud fue enviada para aprobación.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancel(int id)
    {
        var currentUser = GetCurrentUser();
        if (_vacationRequests.Cancel(id, currentUser.Id, out var errorMessage))
        {
            TempData["Success"] = "La solicitud fue cancelada.";
        }
        else
        {
            TempData["Error"] = errorMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        var request = _vacationRequests.GetRequestDetails(id);
        if (request == null)
        {
            return NotFound();
        }
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveBoss(int id, string comment)
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role != "Jefe")
        {
            TempData["Error"] = "No tienes permiso de Jefe para realizar esta acción.";
            return RedirectToAction(nameof(Index));
        }

        if (!_vacationRequests.ApproveByBoss(id, currentUser.Name, comment, out var errorMessage))
        {
            TempData["Error"] = errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Solicitud aprobada con éxito y enviada a RRHH.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejectBoss(int id, string comment)
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role != "Jefe")
        {
            TempData["Error"] = "No tienes permiso de Jefe para realizar esta acción.";
            return RedirectToAction(nameof(Index));
        }

        if (!_vacationRequests.RejectByBoss(id, currentUser.Name, comment, out var errorMessage))
        {
            TempData["Error"] = errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Solicitud rechazada con éxito.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmHR(int id, string comment)
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role != "RRHH")
        {
            TempData["Error"] = "No tienes permiso de RRHH para realizar esta acción.";
            return RedirectToAction(nameof(Index));
        }

        if (!_vacationRequests.ConfirmByHR(id, currentUser.Name, comment, out var errorMessage))
        {
            TempData["Error"] = errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Solicitud confirmada con éxito. Días descontados del saldo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejectHR(int id, string comment)
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role != "RRHH")
        {
            TempData["Error"] = "No tienes permiso de RRHH para realizar esta acción.";
            return RedirectToAction(nameof(Index));
        }

        if (!_vacationRequests.RejectByHR(id, currentUser.Name, comment, out var errorMessage))
        {
            TempData["Error"] = errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Solicitud rechazada con éxito.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Retorna la lista de festivos de Colombia como JSON para el calendario.</summary>
    [HttpGet]
    public IActionResult Holidays()
    {
        var currentYear = DateTime.Today.Year;
        var holidays = _holidayService.GetHolidayDates([currentYear, currentYear + 1]);
        return Json(holidays);
    }

    /// <summary>El Jefe asigna/quita días disponibles a un empleado.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AdjustDays(int employeeId, int days)
    {
        var currentUser = GetCurrentUser();
        if (currentUser.Role != "Jefe")
        {
            TempData["Error"] = "Solo el Jefe puede ajustar días.";
            return RedirectToAction(nameof(Index));
        }
        if (!_vacationRequests.AdjustDays(employeeId, days, out var errorMessage))
        {
            TempData["Error"] = errorMessage;
        }
        else
        {
            TempData["Success"] = $"Días ajustados correctamente. Nuevo saldo: {_vacationRequests.GetAvailableDays(employeeId)} días.";
        }
        return RedirectToAction(nameof(Index), new { tab = "team" });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
