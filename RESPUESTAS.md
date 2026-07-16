# Respuestas — Sección 4

---

### 1. ¿Usaste IA para construir la solución? ¿Cuál(es) y para qué exactamente?

Sí. Usé **Claude (opencode)** como asistente durante todo el proceso:

- Generación del esqueleto del proyecto ASP.NET Core MVC (modelos, controlador, servicio, vistas).
- Diseño de UI con Bootstrap 5 (dashboard, formulario, tarjeta de saldo).
- Construcción del calendario dual custom en vanilla JS para reemplazar los datepickers nativos.
- Creación de este README y las respuestas.

No usé IA dentro del producto final (no hay un feature de IA generativa en la app), porque no aportaba valor al problema concreto: gestionar solicitudes de vacaciones de forma determinística.

---

### 2. Si usaste IA, cuéntanos una vez que te dio algo mal o incompleto: ¿cómo te diste cuenta y qué hiciste?

**Caso 1 — Calendario nativo:** Claude sugirió inicialmente `<input type="date">` para los campos de fecha. Al probarlo me di cuenta de que: (a) no se distinguía visualmente cuál era inicio y cuál era fin, y (b) al escribir manualmente se abría el datepicker nativo del navegador, que resultaba confuso. Lo reemplacé por un calendario dual visible que muestra ambos meses y deja claro el rango.

**Caso 2 — Validación client-side:** Claude generó los hidden inputs para binding del modelo, pero no consideró que jQuery Validation ignora los inputs ocultos por defecto. Al probar el envío del formulario sin seleccionar fechas, no aparecían los mensajes de error. Lo corregí sobrescribiendo el setting `ignore` del validador.

**Caso 3 — Diseño responsive:** La primera versión del calendario dual no se veía bien en pantallas angostas. Claude no anticipó el breakpoint, así que agregué manualmente un media query para apilar los meses verticalmente en mobile.

---

### 3. ¿Por qué elegiste esta arquitectura y este stack? ¿Qué alternativa descartaste y por qué?

**Arquitectura MVC clásica** porque la aplicación tiene exactamente 3 vistas (dashboard, formulario, privacidad). Un SPA (React/Vue) hubiera agregado complejidad de tooling, estado client-side y bundling sin aportar beneficio real.

**Stack .NET 10 + Bootstrap 5** porque:
- Es el framework web corporativo más común en el ecosistema .NET.
- Bootstrap da una base responsiva probada sin necesidad de un diseñador UI.
- La validación client-side con jQuery Validation es declarativa y se integra naturalmente con los tag helpers de ASP.NET.

**Alternativas descartadas:**
- **Blazor**: Curva de aprendizaje innecesaria para 3 pantallas. El modelo de conectividad (WebSocket en Blazor Server o WASM en Blazor WASM) agrega latencia y complejidad.
- **React/Next.js**: Sobredimensionado. La app no necesita estado global complejo, ruteo del lado del cliente ni renderizado híbrido.
- **SQLite**: Se evaluó para simplificar el setup, pero SQL Server ya está instalado y EF Core maneja las migraciones sin esfuerzo adicional.

---

### 4. ¿Cómo garantizas que el control de días y los estados del flujo sean siempre correctos, sin importar el orden en que ocurran las acciones? (determinismo)

El determinismo se logra con tres decisiones de diseño:

1. **Validación en el servidor (punto único de verdad):** El controlador valida que `EndDate >= StartDate` antes de crear la solicitud. El servicio `CountWeekdays` itera el rango de forma lineal — siempre produce el mismo resultado para las mismas entradas. No hay estado compartido ni efectos secundarios durante el cálculo.

2. **Máquina de estados simple:** El enum `VacationRequestStatus` define 4 estados y solo hay 2 transiciones permitidas:
   - `Pending → Cancelled` (solo si está Pendiente)
   - `Pending → Approved/Rejected` (reservado para workflow futuro)
   El método `Cancel()` en el servicio verifica explícitamente `status == Pending` antes de cancelar. Cualquier otra acción es ignorada (no hay errores silenciosos).

3. **Inmutabilidad de datos creados:** `StartDate`, `EndDate`, `WorkingDays` y `Reason` son `init` en el modelo — no se pueden modificar después de creados. Solo `Status` tiene setter porque es la única propiedad que cambia en el tiempo.

El flujo es determinístico porque:
- `CountWeekdays(A, B)` siempre retorna el mismo valor para los mismos A y B
- Cancelar una solicitud ya cancelada no tiene efecto (no lanza error, no cambia nada)
- El orden de las operaciones no importa: cada acción es atómica y validada contra el estado actual

---

### 5. ¿Le pusiste IA al producto? Si sí, ¿dónde, por qué ahí y cómo evitas que se equivoque? Si no, ¿por qué decidiste que no aportaba?

No. Este producto resuelve un problema transaccional determinístico (crear, listar, cancelar solicitudes de vacaciones) donde la IA generativa no aporta valor y sí introduce riesgos:

- **Alucinaciones**: Una IA podría sugerir fechas incorrectas o estados inválidos.
- **No determinismo**: El cálculo de días hábiles y la validación de fechas deben ser exactos al 100%. Un modelo de lenguaje no puede garantizar eso.
- **Complejidad innecesaria**: Agregar un chat o sugerencias automáticas alargaría el desarrollo sin resolver el problema central.

Si el producto escalara, un lugar donde tendría sentido agregar IA sería en un **módulo de detección de patrones** (ej. "este empleado siempre pide vacaciones en las mismas fechas, ¿quieres sugerirle planificar con anticipación?"), pero incluso eso sería un adicional, no parte del flujo crítico.

---

### 6. Si esto se usara de verdad con 200 empleados, ¿qué cambiarías o qué se rompería?

**Lo que se rompería hoy:**

1. **Persistencia en memoria**: Al reiniciar el servidor se pierden todas las solicitudes. Con 200 empleados es inaceptable — ya está implementado SQL Server con EF Core, pero habría que migrar a Azure SQL para alta disponibilidad.
2. **Sin autenticación**: Cualquiera puede crear/cancelar solicitudes de cualquiera. Habría que agregar login (ASP.NET Core Identity + cookies/JWT) y autorización por roles (empleado vs. líder vs. RRHH).
3. **Sin concurrencia**: La lista `List<VacationRequest>` no es thread-safe. Dos cancelaciones simultáneas podrían causar race conditions. Habría que usar `ConcurrentDictionary` o, mejor, que la BD maneje la concurrencia.
4. **Sin notificaciones**: El líder no recibe aviso cuando alguien solicita vacaciones. Habría que integrar email (SendGrid/MailKit) o notificaciones in-app.

**Lo que cambiaría:**

| Aspecto | Hoy | Con 200 empleados |
|---------|-----|-------------------|
| Autenticación | None | ASP.NET Core Identity + OAuth |
| UI | Calendario dual simple | Calendario con vista de equipo (quién está de vacaciones) |
| Workflow | Solo cancelación | Aprobación/rechazo por líder, reglas de negocio (mínimo de personal por área) |
| Performance | Sin caché | Redis para consultas frecuentes (saldo disponible) + API de festivos |
| Monitoreo | Nada | Application Insights (errores, latencia, uso) |

---

### 7. ¿Qué fue lo más difícil y cómo lo resolviste?

Sin duda **el frontend** fue lo más complejo. No estaba en mi zona de confort y requirió varias iteraciones:

1. **El calendario dual**: Implementar un selector de rango con dos calendarios visibles, manejo de estados (selección inicio/fin/rango intermedio), navegación entre meses, y diseño responsive. Las decisiones de UX (qué pasa si clickeas antes del inicio, cómo reiniciar el rango) requirieron prueba y error.
2. **La interfaz del calendario en general**: Que se viera bien, que fuera intuitiva, que funcionara en mobile, que los colores del rango seleccionado se distinguieran claramente.
3. **Implementación de los holidays (días festivos)**: Aunque el cálculo de días hábiles excluye sábados y domingos, modelar la lógica para que sea extensible a días festivos requirió pensar la estructura de datos desde el principio.
4. **La API**: Coordinar el frontend (calendario en JS) con el backend (ASP.NET MVC). Los hidden inputs para binding, la validación client-side que no funcionaba con inputs ocultos, y asegurarse de que el servidor siempre tenga la última palabra en el cálculo de días hábiles.

La resolución fue iterativa: prototipaba, probaba manualmente, identificaba edge cases y ajustaba. Lo que más ayudó fue separar claramente la responsabilidad — el frontend se encarga de la experiencia de selección, el backend del cálculo y validación final.

---

### 8. Si tuvieras más tiempo, ¿qué le agregarías?

1. **Autenticación y autorización**: Login con ASP.NET Core Identity, roles (empleado, líder, RRHH).
2. **Workflow de aprobación**: El líder ve las solicitudes pendientes de su equipo y puede aprobar/rechazar.
3. **Reglas de negocio**: Mínimo de personas por área, topes por temporada, validación de días consecutivos.
4. **Vista de calendario de equipo**: Solapamiento de vacaciones para evitar que todo un equipo esté ausente.
5. **Notificaciones por email**: Al crear, aprobar o cancelar una solicitud.
6. **Pruebas automatizadas**: Unit tests para `CountWeekdays` y el servicio, integration tests para el controlador, y quizás un par de E2E con Playwright para el flujo crítico.

---

### 9. ¿Cuánto tiempo real te tomó? (Y si usaste IA, aprox. qué porcentaje hizo ella)

**Tiempo total: ~12 horas distribuido en varios días.**

Desglose estimado:

| Actividad | Tiempo | Responsable |
|-----------|--------|-------------|
| Esqueleto del proyecto y modelos | 1 h | IA (80%) / humano (20%) |
| Controlador, servicio, lógica de negocio | 1.5 h | IA (60%) / humano (40%) |
| Integración de EF Core + SQL Server + migraciones | 2 h | Humano (100%) |
| Vistas (dashboard, formulario) | 1.5 h | IA (80%) / humano (20%) |
| Calendario dual (JS + CSS) | 3 h | IA (70%) / humano (30%) |
| API de festivos (Nager.Date) + caché JSON | 1.5 h | IA (60%) / humano (40%) |
| Debugging y correcciones | 1 h | Humano (100%) |
| README + RESPUESTAS | 0.5 h | IA (60%) / humano (40%) |

---

### 10. (Bonus) Cuéntanos una idea tuya que probaste aunque no te la pedimos

Implementé un **sistema de caché local de festivos en JSON** (`holidays_cache.json`). La idea fue: en vez de llamar a la API de Nager.Date en cada solicitud, se descarga la lista de festivos una vez por año y se guarda localmente. Si el archivo tiene menos de 30 días, se usa sin llamar a la API. Esto hace que el sistema funcione incluso sin internet y mejora significativamente el tiempo de respuesta de las validaciones.
