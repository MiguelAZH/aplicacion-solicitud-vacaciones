# Solicitud de Vacaciones

Aplicación web para gestionar solicitudes de vacaciones: los empleados pueden ver su saldo disponible, solicitar períodos y cancelar solicitudes pendientes.

---

## Diseño arquitectónico

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          Cliente (Navegador)                             │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  HTML + CSS (Bootstrap 5) + JS (jQuery, Validador, Calendario)   │    │
│  │  - Dashboard: saldo + historial de solicitudes                   │    │
│  │  - Formulario: selector de rango con calendario dual + motivo    │    │
│  └──────────────────────┬───────────────────────────────────────────┘    │
│                          │ HTTP (GET/POST)                               │
└──────────────────────────┼───────────────────────────────────────────────┘
                           │
┌──────────────────────────┼───────────────────────────────────────────────┐
│                      ASP.NET Core MVC (.NET 10)                          │
│  ┌──────────────────────┴────────────────────────────────────────────┐   │
│  │                         HomeController                            │   │
│  │   GET  /               → Index()     → Dashboard                  │   │
│  │   GET  /Home/Create    → Create()    → Formulario                 │   │
│  │   POST /Home/Create    → Create()    → Guardar solicitud          │   │
│  │   POST /Home/Cancel/:id → Cancel()   → Cancelar solicitud         │   │
│  └──────────────────────┬────────────────────────────────────────────┘   │
│                          │                                               │
│  ┌──────────────────────┴────────────────────────────────────────────┐   │
│  │                   VacationRequestService (Scoped)                  │  │
│  │   - GetAll()     → Lista ordenada de solicitudes                   │  │
│  │   - Create()     → Calcula días hábiles (con festivos) + guarda    │  │
│  │   - Cancel()     → Cambia estado a Cancelled si está Pendiente     │  │
│  │   - CountWeekdays() → Itera rango, excluye sábados/domingos/festivos│ │
│  └──────────────────────┬────────────────────────────────────────────┘   │
│                          │                                               │
│  ┌──────────────────────┴────────────────────────────────────────────┐   │
│  │               HolidayService (caché local JSON)                   │   │
│  │   - GetHolidaysAsync(year) → Lista de festivos desde Nager.Date   │   │
│  │   - Caché local: holidays_cache.json con vigencia de 30 días      │   │
│  └──────────────────────┬────────────────────────────────────────────┘   │
│                          │                                               │
│  ┌──────────────────────┴────────────────────────────────────────────┐   │
│  │                     EF Core + SQL Server                          │   │
│  │   - DbContext → SolicitudVacacionesDb                             │   │
│  │   - Migrations → Actualización automática al iniciar              │   │
│  │   - Seed data: 2 solicitudes de ejemplo                           │   │
│  └───────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
```

### Flujo completo del sistema (con actores)



| Paso | Empleado | Sistema | Jefe / Líder | RH |
|------|----------|---------|--------------|-----|
| **1** | Abre el dashboard y consulta su saldo de días disponibles e historial | Carga datos desde SQL Server | — | — |
| **2** | Hace clic en "+ Solicitar vacaciones" y llena el formulario (rango de fechas + motivo) | Muestra calendario dual con festivos resaltados vía Nager.Date | — | — |
| **3** | Envía la solicitud | Valida que EndDate >= StartDate, calcula días hábiles, guarda en SQL Server con estado **Pendiente** | — | — |
| **4** | — | Notifica al jefe | Revisa las solicitudes pendientes de su equipo | — |
| **5** | Puede **cancelar** mientras siga en Pendiente | Cambia estado a **Cancelada** | — | — |
| **6** | — | Notifica al empleado si es rechazado | Si **rechaza** → **Rechazada**. Si **aprueba** → pasa a RH con estado **Pre-Aprobada** | — |
| **7** | — | Notifica a RH | — | Revisa las solicitudes **Pre-Aprobadas** por los jefes |
| **8** | — | Guarda decisión final y notifica al empleado | — | Si **aprueba** → **Aprobada**. Si **rechaza** → **Rechazada** |
| **9** | Consulta el resultado final en su historial | Registro actualizado en SQL Server | Consulta historial de su equipo | Consulta histórico global |

**Estados de una solicitud:**
- `Pendiente` → Creada por el empleado, esperando revisión del jefe
- `Pre-Aprobada` → Aceptada por el jefe, esperando revisión de RH
- `Aprobada` → Aceptada por RH (estado final)
- `Rechazada` → Denegada por el jefe o por RH
- `Cancelada` → Anulada por el empleado (solo si está Pendiente)

**Notificaciones (workflow futuro):**
- Al empleado cuando su solicitud cambia de estado
- Al jefe cuando hay nuevas solicitudes pendientes en su equipo
- A RH cuando hay solicitudes Pre-Aprobadas listas para revisión final

---

## Infraestructura propuesta

Para 200 empleados en producción:

| Capa | Servicio | Justificación |
|------|----------|---------------|
| Hosting | Azure App Service (B1) | Plan gratis suficiente; escalable a B2/B3 si crece |
| Base de datos | SQL Server (Azure SQL) | Ya lo tienes instalado y lo dominas; integración nativa con EF Core |
| API externa | Nager.Date | API pública gratuita para festivos de Colombia |
| Caché | Archivo JSON local + Redis (futuro) | Cachea festivos por 30 días; Redis si escala a más empleados |
| CI/CD | GitHub Actions | Build + test + deploy automático en push a main |
| Monitoreo | Application Insights | Logs, métricas, alertas de errores |

---

## Tecnologías elegidas

| Categoría | Tecnología | Por qué |
|-----------|------------|---------|
| Backend | .NET 10, ASP.NET Core MVC, EF Core | Plataforma productiva, tipada y con excelente integración a SQL Server |
| Base de datos | SQL Server (Local / Azure) | Ya lo tienes instalado y lo dominas |
| UI | Razor Views, Bootstrap 5 | Rápido de implementar, responsive y sin dependencias JS pesadas |
| API de festivos | Nager.Date (https://date.nager.at) | API pública, gratuita y con datos de Colombia |
| Validación | jQuery + jQuery Validation | Validación client-side declarativa vía data-attributes |
| Calendario | Calendario dual custom (vanilla JS) | Sin dependencias externas; experiencia clara de rango de fechas |

### Por qué no se usaron otras opciones

- **React / Vue / SPA**: Sobredimensionado para una app de ~3 vistas. El MVC clásico con Bootstrap rinde mejor para este alcance.
- **Blazor**: Curva de aprendizaje innecesaria; el equipo puede no conocerlo. MVC es estándar en el ecosistema .NET.
- **Datepicker de terceros (Flatpickr, Daterangepicker)**: Se evaluó, pero un calendario dual custom da control total sobre la UX sin agregar kB de JS.

---

## Cómo correrlo

### Prerrequisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 con las extensiones de desarrollo web de .NET
- SQL Server (local o Azure) — mantener el servidor corriendo desde SSMS
- SQL Server Management Studio (SSMS)
- Git

### Pasos

1. **Clonar el repositorio**
   ```bash
   git clone https://github.com/MiguelAZH/aplicacion-solicitud-vacaciones.git
   cd aplicacion-solicitud-vacaciones
   ```

2. **Abrir SQL Server y crear la base de datos**
   - Abre SSMS y conecta al servidor local
   - Crea una base llamada `SolicitudVacacionesDb`

3. **Configurar la cadena de conexión**
   - Abre la solución `SolicitudVacaciones.slnx` en Visual Studio 2022
   - Edita `appsettings.Development.json` y agrega la sección `ConnectionStrings`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=SolicitudVacacionesDb;Trusted_Connection=True;TrustServerCertificate=True"
   }
   ```

4. **Aplicar migraciones y crear las tablas**
   ```bash
   dotnet ef database update
   ```
   Esto crea automáticamente las tablas en la base de datos.

5. **Ejecutar la aplicación**
   - Desde Visual Studio 2022: presiona F5 o haz clic en "Run" con perfil HTTP
   - O desde consola:
     ```bash
     dotnet run --project SolicitudVacaciones.Web/SolicitudVacaciones.Web.csproj
     ```
   - La app estará disponible en `http://localhost:5000`

---

## Cómo se usó la IA para construirlo

### Herramientas usadas

- **Claude (opencode)** — Asistente principal durante toda la construcción

### En qué ayudó

1. **Estructura inicial del proyecto**: Creación del esqueleto ASP.NET Core MVC, modelos, controlador y vistas.
2. **UI/UX**: Diseño responsive con Bootstrap, paleta de colores, tarjeta de saldo.
3. **Calendario dual**: Implementación del selector de rango con dos calendarios visibles para reemplazar los inputs nativos problemáticos.
4. **Documentación**: README.

### Correcciones necesarias

- El calendario nativo `<input type="date">` generaba confusión entre inicio y fin, y al escribir manualmente se abría un popup redundante. Se reemplazó por un calendario dual custom.
- La validación client-side (jQuery Validation) ignora inputs ocultos por defecto, por lo que hubo que ajustar la configuración del validador (`ignore: ''`).
- Se corrigió el diseño responsive del calendario dual para que en mobile los meses se apilen verticalmente.
