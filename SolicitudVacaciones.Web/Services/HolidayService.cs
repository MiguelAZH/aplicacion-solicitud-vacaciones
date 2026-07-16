using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SolicitudVacaciones.Web.Services;

public interface IHolidayService
{
    HashSet<DateOnly> GetHolidays(int year);
    /// <summary>Returns holidays for multiple years as a flat list of ISO date strings.</summary>
    IReadOnlyList<string> GetHolidayDates(int[] years);
}

public sealed class HolidayService : IHolidayService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HolidayService> _logger;
    private readonly string _cacheDir;

    // Cache en memoria por proceso
    private static readonly Dictionary<int, HashSet<DateOnly>> _cache = new();

    public HolidayService(HttpClient httpClient, ILogger<HolidayService> logger, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(_cacheDir);
    }

    public HashSet<DateOnly> GetHolidays(int year)
    {
        if (_cache.TryGetValue(year, out var cached))
            return cached;

        // Intentar desde archivo de caché local
        var cacheFile = Path.Combine(_cacheDir, $"holidays_{year}.json");
        if (File.Exists(cacheFile))
        {
            try
            {
                var json = File.ReadAllText(cacheFile);
                var dates = ParseHolidayJson(json);
                _cache[year] = dates;
                return dates;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al leer el archivo de caché de festivos para {Year}.", year);
            }
        }

        // Intentar desde API Nager.Date
        try
        {
            var url = $"https://date.nager.at/api/v3/PublicHolidays/{year}/CO";
            var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var dates = ParseHolidayJson(json);
                // Guardar en caché local
                try { File.WriteAllText(cacheFile, json); } catch { /* no bloquear si falla el guardado */ }
                _cache[year] = dates;
                return dates;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar la API de festivos para {Year}. Usando lista de respaldo.", year);
        }

        // Fallback: festivos fijos de Colombia para 2026 y 2027
        var fallback = GetFallbackHolidays(year);
        _cache[year] = fallback;
        return fallback;
    }

    public IReadOnlyList<string> GetHolidayDates(int[] years)
    {
        var result = new List<string>();
        foreach (var year in years)
        {
            var holidays = GetHolidays(year);
            foreach (var d in holidays)
                result.Add(d.ToString("yyyy-MM-dd"));
        }
        return result;
    }

    private static HashSet<DateOnly> ParseHolidayJson(string json)
    {
        var result = new HashSet<DateOnly>();
        using var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("date", out var dateProp))
            {
                if (DateOnly.TryParse(dateProp.GetString(), out var date))
                    result.Add(date);
            }
        }
        return result;
    }

    private static HashSet<DateOnly> GetFallbackHolidays(int year)
    {
        // Festivos fijos de Colombia (no móviles) + festivos trasladados aproximados
        // para 2026 y 2027 como respaldo sin internet.
        if (year == 2026) return new HashSet<DateOnly>
        {
            new(2026, 1, 1),  // Año Nuevo
            new(2026, 1, 12), // Reyes Magos (trasladado)
            new(2026, 3, 23), // San José (trasladado)
            new(2026, 4, 2),  // Jueves Santo
            new(2026, 4, 3),  // Viernes Santo
            new(2026, 5, 1),  // Día del Trabajo
            new(2026, 5, 18), // Ascensión (trasladado)
            new(2026, 6, 8),  // Corpus Christi (trasladado)
            new(2026, 6, 15), // Sagrado Corazón (trasladado)
            new(2026, 6, 29), // San Pedro y San Pablo (trasladado)
            new(2026, 7, 20), // Independencia
            new(2026, 8, 7),  // Batalla de Boyacá
            new(2026, 8, 17), // Asunción de la Virgen (trasladado)
            new(2026, 10, 12),// Día de la Raza (trasladado)
            new(2026, 11, 2), // Todos los Santos (trasladado)
            new(2026, 11, 16),// Independencia de Cartagena (trasladado)
            new(2026, 12, 8), // Inmaculada Concepción
            new(2026, 12, 25),// Navidad
        };

        if (year == 2027) return new HashSet<DateOnly>
        {
            new(2027, 1, 1),
            new(2027, 1, 11),
            new(2027, 3, 22),
            new(2027, 3, 25),
            new(2027, 3, 26),
            new(2027, 5, 1),
            new(2027, 5, 10),
            new(2027, 5, 31),
            new(2027, 6, 7),
            new(2027, 6, 28),
            new(2027, 7, 20),
            new(2027, 8, 7),
            new(2027, 8, 16),
            new(2027, 10, 18),
            new(2027, 11, 1),
            new(2027, 11, 15),
            new(2027, 12, 8),
            new(2027, 12, 25),
        };

        // Para otros años, devolver conjunto vacío (se usará L-V sin festivos)
        return new HashSet<DateOnly>();
    }
}
