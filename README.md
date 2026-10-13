# Centralizador de ofertas con IA

Base inicial de React con JavaScript y una API ASP.NET Core. MySQL será la persistencia cuando se implemente el primer flujo.

## Requisitos

- Node.js 22.12 o superior compatible con Vite 7.
- SDK .NET 10.

## Frontend

Desde `frontend`:

```bash
npm install
npm run dev
```

Para verificar la compilación: `npm run build`.

## Backend

Desde la raíz:

```bash
dotnet build backend/Offers.Api/Offers.Api.csproj
dotnet run --project backend/Offers.Api/Offers.Api.csproj --urls http://localhost:5000
```

La raíz de la API devuelve 404. Se implementaron las consultas GET /api/offers y GET /api/offers/{id}; el frontend consulta ambos mediante el proxy de Vite.

En el entorno de trabajo se instaló el SDK dentro de `.tools/dotnet`, excluido de Git. Allí puede usarse `.tools/dotnet/dotnet` en lugar de `dotnet` desde la raíz. En otra computadora se necesita instalar el SDK .NET 10.

## Verificación de la base

- Frontend: `npm run build` completado correctamente.
- Backend: compilación .NET 10 completada sin errores ni advertencias.
- API: arranque local y respuesta HTTP 404 en la raíz, esperable mientras no existan endpoints.

## Organización

Las carpetas de features reservan los cinco flujos acordados: captación, publicación, consulta, reportes y reprocesamiento. No representan cinco proyectos independientes.

## Pendiente

El modelo de ofertas, el contexto EF Core y la migración InitialOffers están preparados. La migración se aplicó a una instancia MySQL 8.4.11 local de desarrollo en este entorno. Reportes y métricas de uso siguen pendientes. La integración IA está implementada, pero no está activada ni validada con un proveedor real.

Consultar `CONVENTIONS.md` antes de agregar funcionalidades.

## MySQL y EF Core

Se utiliza MySql.EntityFrameworkCore 10.0.9 y EF Core 10.0.9. La migración crea Offers y OfferBenefits, con relación uno a muchos, importes decimales y fechas opcionales.

Configurar la variable de entorno `ConnectionStrings__Offers` siguiendo `backend/Offers.Api/.env.example`, reemplazando usuario y contraseña. .NET no carga automáticamente archivos .env: exportar la variable en la terminal o configurarla en el entorno de ejecución. No subir credenciales.

Instalar la herramienta local desde la raíz:

```bash
dotnet tool install dotnet-ef --version 10.0.9 --tool-path .tools/ef
```

Con un servidor MySQL 8 disponible y la conexión configurada, aplicar la migración explícitamente:

```bash
.tools/ef/dotnet-ef database update --context OffersDbContext --project backend/Offers.Api
.tools/ef/dotnet-ef database update --context CaptureDbContext --project backend/Offers.Api
```

En Windows, usar el ejecutable equivalente de `.tools/ef`. Si se usa el SDK instalado dentro del proyecto, incluir `.tools/dotnet` en PATH y definir DOTNET_ROOT a esa carpeta antes de ejecutar la herramienta.

La aplicación no aplica migraciones al arrancar. Sin conexión configurada puede iniciar esta base vacía, pero no registra el contexto de persistencia. La fábrica de diseño utiliza una cadena de ejemplo sin credenciales reales para generar migraciones sin servidor; no sirve para aplicar cambios a una base real.

Verificado: compilación, generación de SQL y aplicación de InitialOffers en MySQL real. La conexión local utiliza 127.0.0.1:3307 y la base offers. Los binarios, datos y credenciales están en .tools y backend/Offers.Api/.env, excluidos de Git. Esta instalación no configura MySQL en otra computadora. El servidor se detiene al finalizar la verificación; no se mantiene un servicio permanente.

## Base local en este entorno

Para iniciar temporalmente MySQL, aplicar migraciones pendientes y comprobar la API, ejecutar desde la raíz:

```bash
python3 .tools/configure_mysql.py
```

Este script local utiliza las herramientas instaladas en este entorno y las credenciales privadas; no se versiona. En otra computadora configurar un servidor MySQL propio y la variable de conexión según las instrucciones anteriores. No copiar ni subir archivos de credenciales.

## Consulta pública implementada

- Listado y detalle sin registro (HU23 y HU24).
- Código seleccionable, indicación de ausencia confirmada y acceso a la fuente (HU25–HU27).
- Solo se consultan ofertas publicadas y dentro de las fechas conocidas (HU15).
- Sin MySQL configurado o accesible, la API responde 503; React muestra un error y permite reintentar. Una base accesible sin publicaciones muestra el estado vacío.
- La selección utiliza el parámetro `?offer=GUID`; funciona el botón Volver y la navegación del navegador.
- No se cargan promociones ficticias en el catálogo. Los fixtures de las pruebas se eliminan en un bloque de limpieza.
- Las fechas DATE de MySQL se convierten explícitamente a DateOnly para evitar un fallo de lectura del proveedor. NormalizeDateMapping registra ese mapeo; no altera columnas.

Para desarrollo, ejecutar la API en http://127.0.0.1:5000 con la variable de conexión y luego `npm run dev` en frontend. Vite redirige `/api` a ese puerto. Para otro puerto definir `API_PROXY_TARGET` al ejecutar Vite. El proxy solo aplica al desarrollo: el despliegue deberá servir frontend y `/api` bajo el mismo origen o configurar el servidor correspondiente.

## Comprobaciones

```bash
dotnet run --project backend/Offers.Checks
```

Sin configuración ejecuta los checks de estado, límites de vigencia y fecha argentina. Para probar MySQL y HTTP, configurar `ConnectionStrings__Offers` y `OFFERS_CHECKS_API_URL` apuntando a una API activa con la misma base. Ejecutar solo contra una base de desarrollo: crea ofertas de prueba temporales y las elimina al finalizar.

Se verificaron las reglas, la lectura de varios beneficios y decimales, códigos, fechas y la exclusión del detalle para ofertas no disponibles contra MySQL real. La extracción real con IA requiere configurar una clave y un modelo y validar sus resultados. Reportes y métricas de consultas/clics siguen pendientes.

## Administración de publicaciones

Acceder desde Administración en el catálogo o mediante `/?admin=1`.

- Login, consulta de sesión y cierre de sesión (HU17).
- Listado administrativo y detalle para revisar ofertas pendientes, publicadas y desactivadas (HU18).
- Aviso de ofertas sin vencimiento informado (HU16).
- Publicación y desactivación (HU20 y HU21).
- Publicar no omite el control público de vigencia. No existe carga manual de ofertas.
- El backend valida cadena, enlace HTTP/HTTPS, coherencia de fechas, código y valores de beneficios antes de publicar.

Configurar `Admin__Username` y `Admin__Password` mediante variables de entorno privadas. No existe registro de administradores ni contraseña predeterminada. En este entorno se generó una contraseña aleatoria almacenada únicamente en el archivo local `.env` ignorado por Git.

La sesión utiliza una cookie HttpOnly con SameSite Strict y duración absoluta de dos horas. Cada acción POST administrativa, incluido el login, requiere un token obtenido desde `/api/admin/csrf` y enviado en `X-CSRF-TOKEN`. Se solicita un token nuevo después de cambiar la identidad. No se guardan credenciales ni tokens de sesión en localStorage.

Para HTTP local usar `ASPNETCORE_ENVIRONMENT=Development`. Fuera de desarrollo las cookies requieren HTTPS. Las claves de protección de cookies se guardan en `.local/keys` bajo el directorio de ejecución y se excluyen de Git; conservarlas de forma privada si se necesita mantener sesiones entre reinicios.

Las pruebas de integración también verifican accesos sin sesión, credenciales incorrectas, CSRF, publicación, desactivación, rechazo de enlaces inválidos y cierre de sesión. Para ejecutar esa parte, proporcionar las mismas variables administrativas a la API y a Offers.Checks.

## Captación automática y reprocesamiento

La API incluye un proceso periódico (24 horas por defecto) y las acciones protegidas Leer fuentes ahora y Volver a leer la fuente de una oferta. Las ejecuciones registran fuente, inicio, finalización, etapa, resultado, cantidad procesada y error. CaptureDbContext es dueño de CaptureRuns y utiliza su propio historial de migraciones; ExtractedOfferWriter es la operación de publicaciones utilizada por captación para guardar ofertas.

Las fuentes iniciales están en `backend/Offers.Api/appsettings.json`. Se consultan páginas públicas oficiales, sin sesiones de consumidores ni selección automática de ubicación. Se obtiene texto renderizado, una captura de la página para lectura visual/OCR y hasta tres PDF enlazados en dominios oficiales. Las fechas relativas permanecen en las condiciones; no se convierten en fechas inventadas.

El adaptador usa OpenAI Responses con esquema JSON estricto, entrada de imágenes y archivos PDF. Las fuentes se tratan como datos, no como instrucciones. Se rechazan respuestas incompletas, clasificaciones inciertas, valores inválidos y enlaces que no estén presentes en la lectura. Las ofertas nuevas quedan Pending. Una modificación de una oferta Published vuelve a Pending; Disabled se conserva. Una fuente sin ofertas o una lectura fallida no elimina registros anteriores.

La identidad usa la fuente configurada y el identificador publicado cuando existe; de lo contrario, el título original normalizado. Se detectan colisiones dentro de una lectura y hay un índice único en MySQL. Si la fuente cambia su identificador o su título sin un identificador estable, la correspondencia puede requerir revisión: no se implementa deduplicación semántica avanzada.

### Activación

Definir en el entorno privado de la API:

- `Capture__Enabled=true`.
- `Capture__ApiKey`: clave de API del proveedor, nunca en Git ni en el frontend.
- `Capture__Model`: identificador de un modelo compatible con imágenes, PDF y Structured Outputs.
- `Capture__IntervalHours`: intervalo de 1 a 168 horas; valor inicial 24.

No hay clave ni modelo predeterminados. Sin ambos y sin habilitación explícita no se hacen llamadas de IA. La clave de API no se obtiene de la sesión de ChatGPT.

Chromium debe estar instalado para la lectura renderizada. Después de compilar, instalarlo con el script de Playwright generado, indicando una carpeta de navegadores local al proyecto:

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH = "$PWD/.tools/browsers"
pwsh backend/Offers.Api/bin/Debug/net10.0/playwright.ps1 install chromium --only-shell
```

También puede configurarse `Capture__BrowserExecutablePath` con la ruta de un Chromium compatible ya instalado. Si se utiliza un Node propio para el driver, configurar `PLAYWRIGHT_NODEJS_PATH`. En este entorno Chromium quedó instalado dentro de `.tools/chromium`; esos binarios no se suben a Git. `Capture__ProxyServer` es opcional para entornos con proxy.

La lectura del navegador canaliza descargas GET/HEAD por el cliente HTTP, restringe URLs a HTTPS y bloquea destinos privados fuera de los dominios oficiales/configurados. No envía formularios ni pedidos y no ejecuta consultas POST de sitios externos. Algunas promociones que requieran sesión, ubicación, interacciones o APIs POST pueden no estar accesibles; deben aparecer como lectura incierta o error y necesitan validación con la fuente.

### Verificación realizada y límites

- Backend y React compilan.
- La lectura en navegador obtuvo texto y capturas de las tres fuentes oficiales seleccionadas.
- Se verificó el contrato del adaptador con respuestas simuladas, incluidos los campos de entrada para imagen/PDF y rechazo de salida incompleta.
- Se probó contra MySQL el flujo de captación con fuentes y proveedor controlados, sin duplicados, conservación de Disabled, revisión de cambios publicados y registro de fallos.
- No se hicieron llamadas pagas de IA. No se validaron todavía la precisión del OCR ni la cobertura de promociones de un modelo real. El catálogo no contiene ofertas reales extraídas.

Para repetir las lecturas externas, con Chromium configurado y acceso de red:

```bash
OFFERS_CHECKS_LIVE_SOURCES=true dotnet run --project backend/Offers.Checks
```

La ejecución ordinaria de Offers.Checks no llama a la API real de IA. Las verificaciones MySQL usan únicamente una base de desarrollo y eliminan sus fixtures al finalizar.
