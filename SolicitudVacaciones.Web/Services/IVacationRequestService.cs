using System.Collections.Generic;
using SolicitudVacaciones.Web.Models;

namespace SolicitudVacaciones.Web.Services;

public interface IVacationRequestService
{
    IReadOnlyList<Employee> GetEmployees();
    Employee? GetEmployeeById(int id);
    
    IReadOnlyList<VacationRequest> GetRequestsForEmployee(int employeeId);
    IReadOnlyList<VacationRequest> GetRequestsPendingBoss();
    IReadOnlyList<VacationRequest> GetRequestsPendingHR();
    IReadOnlyList<VacationRequest> GetReviewedByBoss();
    IReadOnlyList<VacationRequest> GetReviewedByHR();
    
    IReadOnlyList<Employee> GetSubordinates(int bossId);
    bool AdjustDays(int employeeId, int days, out string errorMessage);
    
    int GetAvailableDays(int employeeId);
    bool HasPendingRequest(int employeeId);
    
    bool TryCreate(int employeeId, VacationRequestInputModel input, out string errorMessage);
    bool Cancel(int id, int employeeId, out string errorMessage);
    
    bool ApproveByBoss(int id, string bossName, string comment, out string errorMessage);
    bool RejectByBoss(int id, string bossName, string comment, out string errorMessage);
    
    bool ConfirmByHR(int id, string hrName, string comment, out string errorMessage);
    bool RejectByHR(int id, string hrName, string comment, out string errorMessage);
    
    VacationRequest? GetRequestDetails(int id);
}
