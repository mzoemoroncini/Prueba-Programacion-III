# Centralizador de Ofertas y Descuentos con IA

Aplicación académica con React/Vite, ASP.NET Core 10 y MySQL 8.4. Un proceso automático lee las fuentes configuradas de McDonald’s, Burger King y Mostaza, utiliza OCR e IA para estructurar promociones y guarda los resultados pendientes de aprobación. El consumidor consulta publicaciones, obtiene códigos como texto seleccionable, abre la fuente y reporta problemas sin registrarse.

No hay carga manual de ofertas, promociones ficticias, botón específico para copiar códigos, filtros, compras ni canjes internos. El alcance está en [MVP](MVP_Centralizador_Ofertas_IA.md), [resumen](Resumen_MVP_Centralizador_Ofertas_IA.md) y [convenciones](CONVENTIONS.md).

## Requisitos para Windows

- Windows de 64 bits y Visual Studio compatible con .NET 10 (por ejemplo Visual Studio 2026), con la carga de trabajo **ASP.NET y desarrollo web**.
- SDK .NET 10 estable. `global.json` permite las actualizaciones de la misma versión principal/secundaria.
- Node.js 22.12 o superior compatible con Vite 7; npm incluido.
- MySQL Server 8.4 iniciado. MySQL Workbench resulta útil para crear la base y el usuario.
- PowerShell 7 (`pwsh`) para el script de preparación y la instalación de Chromium.
- Claves propias de Gemini y de OCR.space para validar la extracción externa. El catálogo, la administración y los reportes no requieren llamar a esos proveedores.

**Docker y Python no son requisitos para ejecutar en Windows con MySQL instalado.** Se ofrecen como alternativa de desarrollo en otro apartado.

Comprobar en una terminal nueva que `dotnet --info`, `node --version`, `npm --version` y `pwsh --version` funcionan. Visual Studio debe usar un SDK compatible con net10.0.

## Primera ejecución en Windows, desde cero

### 1. Crear la base y el usuario de aplicación

En MySQL Workbench, conectarse como administrador de MySQL y abrir `deployment/create-database.sql.example`. Reemplazar la contraseña de ejemplo por una propia antes de ejecutar. El script crea la base `offers` y un usuario `offers_app` limitado a esa base, con permisos para aplicar migraciones. No guardar en Git una copia con la contraseña real.

Si la base o el usuario ya existen, reutilizarlos; `CREATE USER IF NOT EXISTS` no cambia una contraseña existente. No borrar una base existente para actualizar el esquema. Verificar que MySQL esté iniciado desde **Servicios** de Windows y confirmar su puerto, normalmente 3306.

### 2. Preparar dependencias, secretos y migraciones

Desde PowerShell 7, en la carpeta que contiene `DescuentosIA.sln`:

```powershell
pwsh -File scripts/setup-windows.ps1 -MySqlServer 127.0.0.1 -MySqlPort 3306 -Database offers -InstallBrowser
```

El script restaura NuGet, compila C#, ejecuta `npm ci` y compila React. Solicita de forma interactiva el usuario/contraseña de MySQL y el administrador de la aplicación; la contraseña administrativa debe tener entre 12 y 1024 caracteres. Guarda los secretos con **User Secrets de .NET**, fuera del repositorio, pasándolos por stdin y sin imprimirlos. Aplica las migraciones e inicializa cadenas, fuentes y administrador. MySQL conserva el **hash** de la contraseña administrativa, nunca su texto original.

`-InstallBrowser` instala Chromium mediante Playwright en la caché local del usuario de Windows. Requiere red y puede omitirse hasta configurar la extracción. Si se usa `PLAYWRIGHT_BROWSERS_PATH`, el mismo valor debe estar disponible al instalar el navegador y al iniciar la API desde Visual Studio. También se admite `Capture:BrowserExecutablePath` para un Chromium compatible instalado por separado.

El script no crea usuarios del servidor MySQL ni instala MySQL por su cuenta. Una migración fallida detiene la preparación y requiere corregir conexión o permisos. Repetir la preparación reutiliza la base; no la recrea. `--migrate` sincroniza las fuentes y puede actualizar el hash del administrador configurado.

User Secrets es almacenamiento local de desarrollo; no es un almacén cifrado de producción. No compartir su archivo ni usar claves reales en ejemplos versionados.

### 3. Abrir y ejecutar con Visual Studio

1. Abrir **DescuentosIA.sln** desde la raíz.
2. Establecer **Offers.Api** como proyecto de inicio. `Offers.Checks` es el ejecutable de verificaciones, no la API.
3. En **Administrar secretos de usuario** de Offers.Api, comprobar los nombres `ConnectionStrings:Offers`, `Admin:Username` y `Admin:Password`. El script anterior ya los creó para el mismo proyecto.
4. Ejecutar con F5. El perfil Development escucha en el puerto **5000**.
5. En otra terminal, desde la raíz:

```powershell
npm --prefix frontend run dev
```

Vite indica la dirección de la interfaz y reenvía `/api` al puerto 5000. F5 no inicia React ni MySQL. El frontend se ejecuta por separado durante el desarrollo. Usar el mismo navegador/origen para catálogo y administración.

Entrar en **Administración** con las credenciales elegidas en el script. Una base nueva comienza con el catálogo vacío y sin promociones pendientes: no se generan ofertas para simular la extracción.

### 4. Alternativa manual, sin el script de preparación

```powershell
dotnet restore DescuentosIA.sln
dotnet build DescuentosIA.sln
npm --prefix frontend ci
npm --prefix frontend run build
```

Configurar en **Administrar secretos de usuario** del proyecto API un JSON con estas claves y valores propios:

```json
{
  "ConnectionStrings:Offers": "Server=127.0.0.1;Port=3306;Database=offers;User=offers_app;Password=REEMPLAZAR_LOCALMENTE;",
  "Admin:Username": "REEMPLAZAR_LOCALMENTE",
  "Admin:Password": "REEMPLAZAR_POR_UNA_CONTRASENA_DE_AL_MENOS_12_CARACTERES"
}
```

Luego, en PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/Offers.Api -- --migrate
```

El inicio ordinario de la API **no aplica migraciones**. El comando anterior aplica OffersDbContext, CaptureDbContext y ConfigurationDbContext, cada uno con su historial separado. No utilizar `EnsureCreated`, eliminar tablas ni recrear la base para aplicar una actualización. Para cambiar las credenciales de bootstrap o las fuentes configuradas, repetir `--migrate`.

## IA, OCR y lectura automática

En User Secrets de Offers.Api o en las variables externas equivalentes, configurar:

| User Secrets | Variable de entorno | Uso |
| --- | --- | --- |
| `ConnectionStrings:Offers` | `ConnectionStrings__Offers` | Conexión MySQL. |
| `Admin:Username` / `Admin:Password` | `Admin__Username` / `Admin__Password` | Creación/actualización del administrador al migrar. |
| `Capture:Enabled` | `Capture__Enabled` | `false` inicialmente; `true` habilita el proceso programado. |
| `Capture:Provider` | `Capture__Provider` | `Gemini`. El flujo activo no selecciona OpenAI ni hace fallback. |
| `Capture:ApiKey` | `Capture__ApiKey` | Clave privada de un proyecto gratuito de Gemini. |
| `Capture:Model` | `Capture__Model` | Modelo disponible en la cuenta con salida estructurada. Sin valor predeterminado. |
| `Capture:IntervalHours` | `Capture__IntervalHours` | Intervalo de 1 a 168 horas; inicial 24. |
| `Ocr:Enabled` | `Ocr__Enabled` | `true` para OCR.space. |
| `Ocr:ApiKey` | `Ocr__ApiKey` | Clave propia de OCR.space. |
| `Capture:BrowserExecutablePath` | `Capture__BrowserExecutablePath` | Chromium alternativo; opcional. |

`backend/Offers.Api/.env.example` documenta las variables sin credenciales reales. **ASP.NET Core no carga archivos .env automáticamente.** Usar User Secrets en desarrollo o variables del proceso; no poner secretos en React, appsettings.json ni launchSettings.json. Las variables `Ai__*` no pertenecen a esta aplicación.

Las fuentes iniciales están en `backend/Offers.Api/appsettings.json`. La lectura valida dominios oficiales, consulta robots.txt, no inicia sesión en los comercios, no envía formularios ni elude bloqueos. Un robots.txt que impide la ruta o no puede confirmarse detiene esa lectura y se registra como fallo. Algunos sitios requieren APIs, selección de sucursal o ubicación; esto puede impedir encontrar todas sus promociones.

El lector obtiene texto renderizado, una captura de la página que incluye sus imágenes visibles y hasta tres PDF oficiales enlazados. OCR.space transforma los archivos en texto; la IA recibe ese texto y un esquema JSON. El contenido de las fuentes se trata como datos, no como instrucciones. Se conservan desconocidos como null, nombres originales, fechas explícitas y valores decimales; no se inventan códigos, precios o vigencias.

La integración OCR rechaza archivos de más de 1 MB, PDF de más de tres páginas, respuestas parciales y errores; no trunca archivos ni desactiva verificaciones para continuar. Son límites de esta implementación que deben contrastarse con el plan del proveedor. Con OCR desactivado, la lectura visual directa requiere un modelo que soporte imágenes y PDF.

La captación programada y **Leer fuentes ahora / Volver a leer la fuente** utilizan el mismo coordinador. Sin habilitación, claves, modelo y conexión válidos no se ejecuta. Una lectura fallida conserva las ofertas anteriores y registra la etapa. Nuevas ofertas: Pending; cambios sobre Published: vuelven a Pending; Disabled nunca se reactiva automáticamente. Las identidades usan fuente + ID publicado, o título normalizado si no hay ID, con índice único en MySQL. Cambiar un título sin ID estable puede necesitar revisión; no existe deduplicación semántica avanzada.

No hay claves de demostración ni llamadas reales en las pruebas ordinarias. Las credenciales no se obtienen de una sesión de ChatGPT. Consultar la cuota/costo y la disponibilidad del modelo antes de habilitar lecturas.

Documentación: [Gemini](https://ai.google.dev/gemini-api/docs/structured-output), [OCR.space](https://ocr.space/ocrapi).

### Gemini en el nivel gratuito

El proveedor principal y único del flujo activo es **Gemini Developer API oficial**, en `generativelanguage.googleapis.com`, usando `generateContent`, clave en cabecera y salida JSON estructurada. No se utilizan Vertex AI, facturación, aprovisionamiento de servicios, grounding, caché de pago ni fallback automático a otro proveedor. El adaptador legado OpenAI no está registrado en el runtime.

Se revisó la configuración del entorno: los nombres antiguos `Ai__Enabled` y `Ai__Model` no configuran esta aplicación. Se requieren `Capture__ApiKey`, `Capture__Model` y `Ocr__ApiKey`; los campos seguros del entorno para las claves son `CAPTURE_API_KEY` y `OCR_SPACE_API_KEY`. No enviar claves por chat ni guardarlas en Git. En Windows, los nombres equivalentes en User Secrets usan `:`.

La tabla oficial de precios incluye `gemini-2.5-flash-lite` con entrada/salida gratuitas en Free tier; es un ejemplo configurable, no una garantía de disponibilidad de tu cuenta. Elegir un modelo con salida estructurada y **proyecto Free tier sin facturación vinculada**, confirmar su disponibilidad y sus cuotas en Google AI Studio. La aplicación no activa facturación; tampoco puede certificar desde una clave que un proyecto externo nunca haya sido asociado a un plan pago. No usar claves de proyectos pagos si el objetivo es costo cero.

Desde PowerShell 7, después de preparar MySQL y la aplicación:

```powershell
pwsh -File scripts/configure-gemini.ps1 -Model gemini-2.5-flash-lite
```

Solicita ambas claves ocultando su entrada y las guarda por stdin en User Secrets. Deja la captación deshabilitada por defecto. Para habilitarla expresamente, repetir con `-EnableCapture` o cambiar `Capture:Enabled` a `true` y reiniciar la API. La primera lectura automática puede comenzar al arrancar.

El presupuesto local inicial es **10 intentos por día y al menos 60 segundos entre llamadas**:

- `Capture__MaxRequestsPerDay` / `Capture:MaxRequestsPerDay`: máximo local diario, incluidos intentos fallidos.
- `Capture__MinRequestIntervalSeconds` / `Capture:MinRequestIntervalSeconds`: separación mínima; no admite menos de 60 segundos.
- El contador y la espera posterior a un 429 se guardan en MySQL. Reiniciar la aplicación no reinicia la cuota.
- El día de cuota usa medianoche de la zona del Pacífico, como documenta Gemini. Se bloquea la fila al reservar para evitar superar el presupuesto entre procesos de la misma instalación.
- Un HTTP 429 registra `GeminiQuota`, respeta `Retry-After` o `retryDelay` y suspende nuevos intentos. No hay reintentos inmediatos. El presupuesto diario local registra `GeminiDailyBudget`.
- Se revisa el presupuesto antes de leer más fuentes/OCR y se reserva cada intento antes del HTTP. Una espera de ritmo se realiza sin mantener bloqueos SQL.
- El panel muestra el código de error y la fecha permitida para un nuevo intento; los registros previos de ofertas se conservan.

Las cuotas reales son por **proyecto**, varían por modelo y contemplan RPM, TPM y RPD. Los valores locales son conservadores, no una reproducción exacta de los límites de Google, y no conocen el uso de otras aplicaciones del mismo proyecto. Consultar los límites activos en AI Studio y ajustar hacia abajo si hace falta. La API sigue siendo la autoridad para los límites de tokens y capacidad. No aumentar límites ni activar pagos automáticamente al recibir un error.

Referencias oficiales: [precios](https://ai.google.dev/gemini-api/docs/pricing), [cuotas y reinicio diario](https://ai.google.dev/gemini-api/docs/rate-limits), [salida estructurada](https://ai.google.dev/gemini-api/docs/structured-output).

## Recorrido completo de validación real

1. Configurar MySQL y aplicar todas las migraciones. `/health` debe responder 200: comprueba conectividad y ausencia de migraciones pendientes de los tres contextos.
2. Iniciar API y React; entrar como administrador.
3. Instalar Chromium y configurar claves/modelo. Activar `Capture:Enabled` y reiniciar la API. Esto puede consumir cuota al comenzar la primera lectura.
4. Ejecutar **Leer fuentes ahora** si no hay una lectura en curso. Revisar estado, etapa, fecha, cantidad procesada y errores por fuente.
5. Contrastar cada promoción obtenida con la fuente real, incluidas las condiciones y archivos. Las pruebas de contrato no certifican precisión factual del proveedor.
6. Aprobar una oferta pendiente y comprobar su aparición pública según la fecha argentina. Desactivarla y confirmar su exclusión.
7. Releer una fuente y verificar que no duplique ni reactive ofertas desactivadas.
8. Desde el catálogo, abrir un detalle, seleccionar el código si existe, abrir el origen y enviar un reporte. Administración debe mostrar motivo, comentario opcional y fecha; el reporte no debe desactivar automáticamente la oferta.

**La extracción real con IA y OCR solo se considera validada al completar este recorrido con proveedores y fuentes reales.** El catálogo puede permanecer vacío si no hay credenciales, hay restricciones de acceso o las fuentes no devuelven promociones verificables.

## Compilación y pruebas

Compilar desde PowerShell o la terminal de Visual Studio:

```powershell
dotnet build DescuentosIA.sln
npm --prefix frontend run build
```

Con la API de desarrollo en el puerto 5000 y la misma conexión en User Secrets:

```powershell
$env:OFFERS_CHECKS_API_URL = "http://127.0.0.1:5000"
dotnet run --project backend/Offers.Checks
```

Offers.Checks carga los User Secrets de la API y las variables de entorno. Sin conexión o URL informa que omitió integración; ese resultado no valida HTTP/MySQL. Antes de la suite, mantener `Capture:Enabled=false` en la API de pruebas para evitar lecturas externas concurrentes. La suite verifica reglas de vigencia, estados, contratos IA/OCR, respuestas incompletas, persistencia, autenticación, CSRF, autorización, publicación, desactivación, duplicados, reportes y métricas. Crea fixtures únicamente para la prueba y los limpia al finalizar. **Ejecutar contra una base de desarrollo, nunca producción.**

Prueba opcional de navegador, con API y Vite iniciados:

```powershell
npm install --prefix .tools/browser-tools --no-save playwright@1.63.0
$env:OFFERS_CHECKS_BROWSER = "true"
$env:OFFERS_CHECKS_FRONTEND_URL = "http://localhost:5173"
$env:OFFERS_CHECKS_API_URL = "http://127.0.0.1:5000"
dotnet run --project backend/Offers.Checks
```

Usa Chromium instalado anteriormente. El test abre su fixture temporal, prueba pantalla móvil, reporte, enlace, revisión administrativa y logout. No agrega carga manual al producto ni deja datos ficticios en el catálogo. Las capturas se guardan en `.tools/screenshots`, ignorado por Git.

Verificación externa de lectura de fuentes sin IA/OCR:

```powershell
$env:OFFERS_CHECKS_LIVE_SOURCES = "true"
dotnet run --project backend/Offers.Checks
```

No activar simultáneamente flags de navegador y fuentes al diagnosticar una dependencia externa. La prueba de Gemini real es separada mediante `OFFERS_CHECKS_LIVE_GEMINI=true`; necesita clave/modelo y consume cuota. La verificación completa de OCR se realiza desde el panel con sus credenciales y documentos reales.

## Alternativa con Docker y Python

Desde la raíz, con Docker Compose y Python 3:

```sh
python scripts/dev.py setup
npm --prefix frontend ci
dotnet restore DescuentosIA.sln
python scripts/dev.py migrate
```

En dos terminales separadas, ejecutar `python scripts/dev.py api` y `python scripts/dev.py frontend`. Esta alternativa usa API **5001**, Vite **5175** y MySQL **3307**. Guarda datos y credenciales aleatorias en `.local`, ignorado por Git. Repetir setup conserva la base y verifica una consulta SQL autenticada. `python scripts/dev.py checks` y `python scripts/dev.py build` usan esa configuración. No depende de instalaciones locales anteriores ni de scripts ausentes en `.tools`.

## Arquitectura y seguridad

- **OfferCapture:** lectura, OCR.space, adaptador oficial Gemini y coordinador periódico.
- **OfferPublication:** persistencia de ofertas/beneficios, deduplicación y transiciones de publicación.
- **OfferBrowsing:** DTOs, catálogo y detalle públicos, vigencia argentina y consultas.
- **OfferReports:** reportes y registro mínimo de accesos al origen.
- **SourceReprocessing:** endpoints administrativos que reutilizan el coordinador.
- **Configuration:** cadenas y fuentes relacionadas, administradores con hash y migraciones propias. Los identificadores de fuente se conservan en ofertas/ejecuciones para mantener el origen histórico; no se borran fuentes deshabilitadas.

Cookie administrativa HttpOnly, SameSite Strict, expiración absoluta de dos horas y HTTPS fuera de Development. Las acciones administrativas exigen autorización y token antiforgery. Login y envíos públicos tienen límites de solicitudes. No se guardan tokens o contraseñas en localStorage. Las claves de protección de cookies están en `.local/keys` bajo la API: protegerlas y conservarlas si se necesitan sesiones entre reinicios.

Los reportes no cambian estados. Las consultas administrativas no cuentan como aperturas públicas. Los clics se registran sin bloquear el enlace y pueden perderse sin conexión; no representan compras ni canjes. El panel muestra hasta 200 reportes y 50 ejecuciones recientes, sin estadísticas avanzadas.

## Solución de problemas

- **SDK no compatible:** revisar `dotnet --list-sdks` y actualizar Visual Studio/carga de trabajo y SDK .NET 10.
- **MySQL no disponible:** comprobar servicio, puerto, base, usuario, contraseña y permisos. `/health` devuelve 503 si falta conexión o migraciones.
- **No aparece el frontend en el puerto de la API:** en desarrollo se sirve con Vite; F5 inicia solo el backend.
- **Error 503 en catálogo:** aplicar migraciones y revisar conexión. No equivale a catálogo vacío.
- **Error 401 / login:** inicializar el administrador con `--migrate`, usar el usuario exacto y comprobar User Secrets del proyecto Offers.Api.
- **Error 400 administrativo:** recargar para obtener un token CSRF vigente. El frontend solicita uno nuevo tras cambiar identidad.
- **Error 429:** se alcanzó un límite de solicitudes o cuota del proveedor; esperar la ventana correspondiente y revisar configuración.
- **Navegador no instalado:** repetir la instalación Playwright con el mismo usuario/ruta usados por la API.
- **Lectura fallida:** revisar etapa y restricciones de la fuente. No eludir bloqueos ni completar con ofertas inventadas.

## Estado de verificación

Los resultados actuales y sus límites están en [docs/VALIDACION.md](docs/VALIDACION.md). La trazabilidad de las 33 issues y sus criterios está en [docs/ISSUES.md](docs/ISSUES.md). Windows/Visual Studio gráfico, despliegue remoto y los proveedores externos no se consideran probados solo por compilar en Linux. Ningún script hace commits, pushes, despliegues ni cierra issues.
