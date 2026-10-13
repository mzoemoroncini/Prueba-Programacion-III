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

La API todavía no contiene endpoints; una respuesta 404 en su raíz es esperable. El frontend aún no consulta la API.

En el entorno de trabajo se instaló el SDK dentro de `.tools/dotnet`, excluido de Git. Allí puede usarse `.tools/dotnet/dotnet` en lugar de `dotnet` desde la raíz. En otra computadora se necesita instalar el SDK .NET 10.

## Verificación de la base

- Frontend: `npm run build` completado correctamente.
- Backend: compilación .NET 10 completada sin errores ni advertencias.
- API: arranque local y respuesta HTTP 404 en la raíz, esperable mientras no existan endpoints.

## Organización

Las carpetas de features reservan los cinco flujos acordados: captación, publicación, consulta, reportes y reprocesamiento. No representan cinco proyectos independientes.

## Pendiente

El modelo de ofertas, el contexto EF Core y la migración InitialOffers están preparados. La migración no se aplicó a un servidor MySQL. Autenticación, endpoints e integración IA siguen pendientes.

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

Verificado: compilación, generación de migración y generación de SQL sin conexión a un servidor. Pendiente: aplicar y probar operaciones en MySQL real.
