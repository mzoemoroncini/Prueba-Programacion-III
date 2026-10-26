# Validación realizada y límites

## Resultado comprobado

Entorno de ejecución: Linux, SDK .NET 10.0.403, Node.js 24.19.0, MySQL 8.4 real y Chromium. Estos resultados no equivalen a una ejecución gráfica en Windows.

- **Backend:** solución DescuentosIA.sln compilada correctamente, cero errores y cero advertencias.
- **Frontend:** instalación con npm ci y compilación Vite correctas.
- **Migraciones:** aplicadas desde una base MySQL nueva y repetidas sin recrear la base ni duplicar fuentes/administrador. Incluyen ofertas/beneficios, ejecuciones, reportes/métricas, configuración y presupuesto Gemini.
- **Pruebas automatizadas:** 117 comprobaciones aprobadas en la ejecución final con navegador habilitado. Incluyen reglas y contratos, HTTP y MySQL reales, autenticación persistida con hash, CSRF, permisos, publicación, desactivación, deduplicación y todos los tipos de beneficio.
- **Navegador real:** catálogo, detalle móvil sin desborde horizontal, código preservado sin botón para copiar, reporte persistido, activación del origen, consulta del reporte en administración, apertura de la oferta asociada y logout. Las promociones usadas son fixtures temporales; se eliminaron al finalizar.
- **Cuota Gemini:** reserva persistida antes del HTTP, separación entre intentos, máximo diario, supervivencia a reinstanciar el servicio, cambio de día, respuesta 429 con cuerpo malformado, respeto de Retry-After, ausencia de reintentos inmediatos y conservación de ofertas ante fallos. El HTTP de estas pruebas es controlado; no consume cuota de Google.
- **Fuentes oficiales reales:** lectura en Chromium de McDonald’s, Burger King y Mostaza, con texto y captura obtenidos respetando la política de acceso. Esto verifica la lectura de esas páginas, no la extracción de promociones con IA ni la fidelidad de OCR.
- **Endpoints:** /health devuelve 200 con conexión y migraciones completas; /api/offers devuelve un array vacío después de limpiar fixtures. Inicio sin conexión configurada comprobado: /health y /api/offers devuelven 503, sin impedir el arranque por errores de inyección de dependencias.
- **Windows:** sintaxis de setup-windows.ps1 y configure-gemini.ps1 validada con el parser real de PowerShell 7. La ruta documentada usa MySQL local y User Secrets, sin requerir Docker ni Python. No se ejecutaron los scripts completos sobre una máquina Windows ni se probó Visual Studio gráfico.

Los logs de esta ejecución están en `.tools/validation-final.log`, `.tools/build-gemini-final.log`, `.tools/live-sources.log` y `.tools/migrations-final.log`, ignorados por Git. Las capturas de prueba están en `.tools/screenshots`. No contienen claves de proveedores ni se incluyen en la distribución del código.

## Integraciones que todavía no se consideran verificadas

**Gemini real:** no hay clave vinculada a Capture__ApiKey ni modelo operativo configurado en esta máquina. No se invocó generateContent con una clave real durante esta implementación. El adaptador oficial, el contrato estructurado y el tratamiento de errores/cuotas están implementados y probados de forma controlada. La interpretación y precisión sobre ofertas reales siguen pendientes.

**OCR.space real:** no hay clave vinculada a Ocr__ApiKey. No se hicieron peticiones reales de OCR. El contrato, procesamiento de imagen/PDF, tamaño, páginas, respuestas parciales y errores están probados con HTTP controlado. La exactitud sobre imágenes/documentos reales sigue pendiente.

**Flujo automático externo completo:** necesita ambas claves, un modelo disponible en un proyecto Gemini Free tier y una ejecución con revisión de resultados. Leer las fuentes o compilar no basta para declarar ese flujo validado. El catálogo no se rellenó con promociones ficticias para ocultar esa limitación.

## Requisitos concretos para la validación externa

Completar de forma segura:

- `Capture__ApiKey`: clave de Gemini de un proyecto Free tier sin facturación vinculada.
- `Capture__Model`: modelo con salida estructurada y cuota gratuita disponible. La tabla oficial incluye gemini-2.5-flash-lite; verificar disponibilidad en la cuenta.
- `Ocr__ApiKey`: clave propia de OCR.space.

Mantener `Capture__Provider=Gemini`, `Ocr__Enabled=true` y el presupuesto conservador. Habilitar `Capture__Enabled=true` solo para la prueba real y reiniciar la API. En Windows, configurar los equivalentes con `:` mediante scripts/configure-gemini.ps1 o Administrar secretos de usuario. En el entorno cloud, las claves se completan en los campos seguros CAPTURE_API_KEY y OCR_SPACE_API_KEY; nunca en el chat.

La aplicación no crea cuentas, no activa facturación, no aumenta cuotas y no cambia automáticamente a un proveedor pago. Los límites de Google son por proyecto/modelo y deben contrastarse con AI Studio; el contador local no conoce consumos de otras aplicaciones.

Este informe registra la validación local previa a publicar el código. No se ejecutaron despliegues ni cierres de issues.
