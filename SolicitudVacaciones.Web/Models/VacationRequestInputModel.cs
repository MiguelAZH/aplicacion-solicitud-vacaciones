using System.ComponentModel.DataAnnotations;

namespace SolicitudVacaciones.Web.Models;

public sealed class VacationRequestInputModel
{
    [Display(Name = "Fecha de inicio")]
    [Required(ErrorMessage = "Selecciona la fecha de inicio.")]
    public DateOnly StartDate { get; set; }

    [Display(Name = "Fecha de finalización")]
    [Required(ErrorMessage = "Selecciona la fecha de finalización.")]
    public DateOnly EndDate { get; set; }

    [Display(Name = "Motivo")]
    [Required(ErrorMessage = "Indica el motivo de la solicitud.")]
    [StringLength(300)]
    public string Reason { get; set; } = string.Empty;
}
