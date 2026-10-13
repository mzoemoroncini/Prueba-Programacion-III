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

El modelo de ofertas, el contexto EF Core y la migración InitialOffers están preparados. La migración se aplicó a una instancia MySQL 8.4.11 local de desarrollo en este entorno. Autenticación, publicación administrativa, reportes, métricas de uso e integración IA siguen pendientes.

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
.tools/ef/dotnet-ef database update --project backend/Offers.Api
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

Se verificaron las reglas, la lectura de varios beneficios y decimales, códigos, fechas y la exclusión del detalle para ofertas no disponibles contra MySQL real. No se consideran cerradas la extracción IA, la publicación administrativa, los reportes ni las métricas de consultas/clics.
