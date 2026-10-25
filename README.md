# Centralizador de ofertas con IA

Base inicial de React con JavaScript y una API ASP.NET Core. MySQL será la persistencia cuando se implemente el primer flujo.

## Requisitos

- Node.js 22.12 o superior compatible con Vite 7.
- SDK .NET 10.

## Abrir en Visual Studio

Abrir `DescuentosIA.sln` desde la raíz con Visual Studio compatible con .NET 10 y la carga de trabajo ASP.NET y desarrollo web instalada. La solución contiene `Offers.Api` (backend) y `Offers.Checks` (verificaciones).

Establecer `Offers.Api` como proyecto de inicio. Su perfil ejecuta la API en `http://localhost:5000` en modo Development. Configurar la conexión MySQL y las claves mediante **Administrar secretos de usuario** del proyecto, usando las secciones y nombres indicados en la configuración de este README. No guardar credenciales en `launchSettings.json`.

React permanece en `frontend` y se ejecuta con `npm install` y `npm run dev` desde la terminal de Visual Studio. Abrir la dirección que indique Vite para usar la interfaz. Iniciar la API no inicia React ni MySQL automáticamente.

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

El modelo de ofertas, el contexto EF Core y la migración InitialOffers están preparados. La migración se aplicó a una instancia MySQL 8.4.11 local de desarrollo en este entorno. Reportes y medición básica de consultas/accesos implementados (HU28–HU33). La integración IA está implementada, pero no está activada ni validada con un proveedor real.

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

Se verificaron las reglas, la lectura de varios beneficios y decimales, códigos, fechas y la exclusión del detalle para ofertas no disponibles contra MySQL real. La extracción real con IA requiere configurar una clave y un modelo y validar sus resultados. Reportes y medición básica de consultas/clics implementados (HU28–HU33).

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

La lectura usa OCR.space para convertir capturas y PDF a texto. Ese texto, junto con el texto de la página y sus enlaces, se envía al proveedor configurado (Gemini por defecto u OpenAI Responses) con un esquema JSON; con OCR habilitado no se envían imágenes ni PDF a la IA. Las fuentes se tratan como datos, no como instrucciones. Se rechazan respuestas incompletas, clasificaciones inciertas, valores inválidos y enlaces que no estén presentes en la lectura. Las ofertas nuevas quedan Pending. Una modificación de una oferta Published vuelve a Pending; Disabled se conserva. Una fuente sin ofertas o una lectura fallida no elimina registros anteriores.

La identidad usa la fuente configurada y el identificador publicado cuando existe; de lo contrario, el título original normalizado. Se detectan colisiones dentro de una lectura y hay un índice único en MySQL. Si la fuente cambia su identificador o su título sin un identificador estable, la correspondencia puede requerir revisión: no se implementa deduplicación semántica avanzada.

### Activación

Definir en el entorno privado de la API:

- `Capture__Enabled=true`.
- `Capture__Provider=Gemini` (también se admite `OpenAI`).
- `Capture__ApiKey`: clave de API del proveedor, nunca en Git ni en el frontend.
- `Capture__Model`: `gemini-3.6-flash` para Gemini, o un modelo compatible con Responses y Structured Outputs para OpenAI. Verificar disponibilidad y cuota en la cuenta del proveedor.
- `Ocr__Enabled=true` (valor inicial).
- `Ocr__ApiKey`: clave propia de OCR.space, privada.
- `Capture__IntervalHours`: intervalo de 1 a 168 horas; valor inicial 24.

No hay clave ni modelo predeterminados. Sin clave de IA, modelo, clave OCR y habilitación explícita no se inicia la captación. La clave de API no se obtiene de la sesión de ChatGPT. El nivel gratuito de Gemini tiene cuotas: un HTTP 429 registra un fallo y conserva las ofertas existentes; no se cambia automáticamente a un proveedor pago.

Para desarrollo local, .NET carga User Secrets con `ASPNETCORE_ENVIRONMENT=Development`:

```bash
dotnet user-secrets set "Capture:Provider" "Gemini" --project backend/Offers.Api
dotnet user-secrets set "Capture:Model" "gemini-3.6-flash" --project backend/Offers.Api
dotnet user-secrets set "Capture:ApiKey" "REEMPLAZAR_LOCALMENTE" --project backend/Offers.Api
```

User Secrets guarda la clave fuera del repositorio y solo se carga en desarrollo. En producción usar variables de entorno privadas. `.env.example` documenta las variables; la API no carga archivos `.env` automáticamente.

Documentación del proveedor: https://ai.google.dev/gemini-api/docs/pricing y https://ai.google.dev/gemini-api/docs/generate-content/structured-output.

Chromium debe estar instalado para la lectura renderizada. Después de compilar, instalarlo con el script de Playwright generado, indicando una carpeta de navegadores local al proyecto:

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH = "$PWD/.tools/browsers"
pwsh backend/Offers.Api/bin/Debug/net10.0/playwright.ps1 install chromium --only-shell
```

También puede configurarse `Capture__BrowserExecutablePath` con la ruta de un Chromium compatible ya instalado. Si se utiliza un Node propio para el driver, configurar `PLAYWRIGHT_NODEJS_PATH`. En este entorno Chromium quedó instalado dentro de `.tools/chromium`; esos binarios no se suben a Git. `Capture__ProxyServer` es opcional para entornos con proxy.

La lectura del navegador canaliza descargas GET/HEAD por el cliente HTTP, restringe URLs a HTTPS y bloquea destinos privados fuera de los dominios oficiales/configurados. No envía formularios ni pedidos y no ejecuta consultas POST de sitios externos. Algunas promociones que requieran sesión, ubicación, interacciones o APIs POST pueden no estar accesibles; deben aparecer como lectura incierta o error y necesitan validación con la fuente.

### OCR.space

La integración utiliza el motor 2 con detección automática de idioma. Se envía un archivo por petición, con la clave en una cabecera. Para el plan gratuito se rechazan archivos vacíos o mayores de 1 MB antes de hacer peticiones; los PDF admiten hasta tres páginas. No se truncan, dividen ni comprimen automáticamente los archivos. Una respuesta parcial, una página fallida o un error del proveedor detiene la extracción y queda registrado en la etapa OCR, conservando las ofertas existentes. No se utilizan claves de demostración.

El plan gratuito publica un límite de 500 peticiones por día por IP y 25.000 al mes. Cada captura y cada PDF consume una petición. Esta integración no administra cuotas; si el proveedor rechaza una petición, registra el fallo. Los límites deben verificarse al elegir el plan en https://ocr.space/ocrapi.

Sin activar OCR, el adaptador conserva la lectura visual directa anterior, que requiere un modelo compatible con imágenes y PDF. La configuración inicial utiliza OCR.space.

### Verificación realizada y límites

- Backend y React compilan.
- La lectura en navegador obtuvo texto y capturas de las tres fuentes oficiales seleccionadas.
- Se verificaron los contratos OCR e IA con respuestas simuladas: imagen y PDF convertidos a texto, IA sin archivos, límites de tamaño, rechazo de OCR parcial y salida IA incompleta.
- Se probó contra MySQL el flujo de captación con fuentes y proveedor controlados, sin duplicados, conservación de Disabled, revisión de cambios publicados y registro de fallos.
- No se hicieron llamadas reales a OCR.space. Se verificó el acceso a Gemini y la disponibilidad de modelos con una clave privada. La prueba de extracción real devolvió HTTP 503 en dos intentos; su resultado sigue pendiente. Los contratos de Gemini se verificaron con respuestas simuladas, incluyendo salidas incompletas, bloqueos, cuotas y errores del proveedor. No se validaron todavía la precisión del OCR ni la cobertura de promociones de un modelo real. El catálogo no contiene ofertas reales extraídas.

Para repetir las lecturas externas, con Chromium configurado y acceso de red:

```bash
OFFERS_CHECKS_LIVE_SOURCES=true dotnet run --project backend/Offers.Checks
```

La ejecución ordinaria de Offers.Checks no llama a la API real de IA. Las verificaciones MySQL usan únicamente una base de desarrollo y eliminan sus fixtures al finalizar.

Para probar Gemini explícitamente con un texto controlado, usando User Secrets o variables de entorno:

```bash
OFFERS_CHECKS_LIVE_GEMINI=true dotnet run --project backend/Offers.Checks
```

Esta prueba llama al proveedor real y consume su cuota. No guarda la oferta de prueba en MySQL. La captación automática permanece deshabilitada hasta configurar la habilitación y las credenciales restantes.


## Despliegue para pruebas en Railway

El Dockerfile de la raíz compila React y .NET 10, incluye el navegador de Playwright 1.63.0 y sirve frontend y `/api` desde el mismo dominio. Conserva MySQL y el backend C#. No utiliza el proxy de Vite en producción.

Crear un proyecto Railway con un servicio MySQL 8 y otro servicio conectado a este repositorio, con la raíz del repositorio como directorio de construcción. `railway.json` declara Dockerfile y la comprobación `/health`. La conexión MySQL queda en la red privada del proyecto; no hace falta exponerla públicamente.

Configurar las variables privadas del servicio de aplicación:

| Variable | Valor |
| --- | --- |
| `ConnectionStrings__Offers` | `Server=${{MySQL.MYSQLHOST}};Port=${{MySQL.MYSQLPORT}};Database=${{MySQL.MYSQLDATABASE}};User=${{MySQL.MYSQLUSER}};Password=${{MySQL.MYSQLPASSWORD}};` |
| `Admin__Username` | Usuario elegido para las pruebas |
| `Admin__Password` | Contraseña privada |
| `Capture__Provider` | `Gemini` |
| `Capture__Model` | `gemini-3.6-flash` |
| `Capture__ApiKey` | Clave privada de Gemini |
| `Capture__Enabled` | `false` hasta comprobar el despliegue; luego `true` para probar captación |
| `Ocr__Enabled` | `true` |
| `Ocr__ApiKey` | Clave privada OCR.space |

Si el servicio de base de datos tiene otro nombre, reemplazar `MySQL` en las referencias. Railway proporciona `PORT`; el contenedor escucha en ese puerto en todas las interfaces. `ASPNETCORE_ENVIRONMENT=Production` viene definido en la imagen, con cookies seguras para el dominio HTTPS.

Al iniciar el contenedor, `deployment/start.sh` ejecuta `--migrate` para aplicar ambos contextos antes de iniciar la API. Una migración fallida impide declarar el servicio listo. Este comportamiento aplica al contenedor; ejecutar la API normalmente en desarrollo no aplica migraciones automáticamente.

Generar un dominio HTTPS para la aplicación desde Railway y entrar al catálogo y a Administración. La base nueva comienza vacía. La prueba completa de extracción de fuentes sigue pendiente por los HTTP 503 observados en Gemini.

Las claves de cookies quedan en `/app/.local/keys`; sin volumen persistente, un redespliegue cierra las sesiones anteriores y requiere iniciar sesión otra vez. Los datos de ofertas permanecen en el volumen del servicio MySQL. Para pruebas, ejecutar una sola réplica de la aplicación.

El contenedor ejecuta la aplicación con un usuario sin privilegios. La comprobación `/health` verifica que la aplicación arrancó después de las migraciones, no la disponibilidad continua de Gemini u OCR.

Verificación local alternativa:

```bash
docker build -t ofertas-ia .
```

La construcción de Docker y el despliegue remoto requieren sus respectivos entornos. No se consideran verificados solo por compilar React y C# por separado. Consultar los créditos y costos de la cuenta Railway antes de habilitar servicios pagos; no se configura un plan pago automáticamente.


## Reportes y medición básica (HU28–HU33)

En el detalle público se puede reportar **Oferta vencida** o **Datos incorrectos**, sin registrarse y con comentario opcional de hasta 2000 caracteres. Se confirma el envío; un reporte no cambia automáticamente el estado de la oferta. Administración muestra los últimos 200 reportes, permite abrir la oferta asociada y conserva la consulta aunque se desactive.

Cada detalle público disponible registra `PublicView` con oferta y fecha UTC. El listado, los detalles fallidos y las consultas administrativas no cuentan como aperturas públicas. Activar el enlace de origen envía `SourceAccess` sin bloquear su apertura; representa un clic, no una carga confirmada ni un canje. El envío del clic es de mejor esfuerzo y puede perderse sin conexión. No se identifica al consumidor ni se agrega un panel estadístico avanzado.

`AddReportsAndInteractions` agrega las tablas OfferReports y OfferInteractions al contexto de publicaciones. Aplicar todas las migraciones antes de ejecutar esta versión. Las nuevas pruebas HTTP/MySQL verifican motivos, comentarios, permisos, persistencia, métricas y conservación del estado de publicación.

## Configuración local reproducible

Requisitos: SDK .NET 10, Node compatible con Vite 7, Python 3 y Docker con Compose. Desde la raíz:

```sh
python scripts/dev.py setup
npm --prefix frontend ci
dotnet restore DescuentosIA.sln
python scripts/dev.py migrate
```

El helper inicia MySQL en el puerto 3307 y conserva sus datos en `.local/mysql`. Genera credenciales aleatorias en `.local/settings.json` y `.local/mysql.env`, ignorados por Git; no los compartas. Repetir setup conserva la configuración y verifica una consulta autenticada antes de declarar MySQL disponible. No recrea bases existentes.

En dos terminales:

```sh
python scripts/dev.py api
python scripts/dev.py frontend
```

Estos comandos se ejecutan por separado: API en el puerto **5001** y Vite en el **5175**, con el proxy configurado automáticamente. La captación permanece deshabilitada hasta configurar sus requisitos reales. El perfil de Visual Studio conserva su puerto 5000 y las instrucciones de User Secrets de las secciones anteriores; no carga automáticamente los archivos privados del helper. Para F5, configurar los mismos valores locales mediante Administrar secretos de usuario.

Con los servicios activos, `python scripts/dev.py checks` ejecuta reglas, contratos y pruebas HTTP/MySQL contra esta base de desarrollo y limpia sus fixtures. `python scripts/dev.py build` compila la solución y React. No ejecutar las pruebas de integración contra producción.
