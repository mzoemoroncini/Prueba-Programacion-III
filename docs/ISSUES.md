# Trazabilidad del MVP y las issues

Se revisaron las 33 issues abiertas de `mzoemoroncini/Prueba-Programacion-III` y los documentos MVP adjuntos. No se cerró ninguna issue ni se realizaron commits/pushes durante esta implementación.

El checkout estaba vacío por el borrado anterior. Se auditó y reutilizó la última implementación del propio historial antes de ese borrado; se completaron persistencia de configuración, autenticación, portabilidad, acceso respetuoso a fuentes, experiencia visual y control de cuota de Gemini. No se tomó código de aplicación del repositorio de MaryAguirre24.

**Criterio de lectura:** implementado no equivale a validado externamente. Las pruebas de contrato controladas no demuestran precisión factual de IA u OCR. Los resultados de ejecución están en [VALIDACION.md](VALIDACION.md).

| Issue | Criterio / implementación | Evidencia y límite |
| --- | --- | --- |
| #1 HU01 | Captación automática: SourceReader → OCR.space → Gemini → validación → MySQL. | Coordinador probado con HTTP controlado y MySQL real; resultado de promociones de Gemini real pendiente. |
| #2 HU02 | Worker periódico y mismo coordinador para ejecución manual administrativa. | Proceso implementado; arranca solo si configuración completa. No se simuló una espera de 24 horas ni una lectura real con claves. |
| #3 HU03 | OCR de captura renderizada que incluye las imágenes de las fuentes. | Contrato, archivos y fallos probados. Precisión de OCR.space real pendiente de clave. |
| #4 HU04 | Lectura de hasta tres PDF oficiales, firma/tamaño y OCR por página. | Contrato y fixtures probados. No se asegura que las fuentes actuales publiquen PDF ni cobertura completa. |
| #5 HU05 | Clasificación y rechazo de extracción incierta. | Respuestas incompletas/bloqueadas/inciertas rechazadas. Clasificación real de Gemini pendiente. |
| #6 HU06 | Resumen en español fiel a fuente y condiciones. | Campo e instrucciones estructuradas implementados. Fidelidad real requiere revisión de resultados externos. |
| #7 HU07 | Interpretación con preservación de condiciones originales. | Contrato y persistencia probados; interpretación factual pendiente de Gemini real. |
| #8 HU08 | Porcentaje, monto fijo, precio promocional, cantidades/2x1, combo, otros. | Todos los tipos guardados y leídos en MySQL en pruebas. |
| #9 HU09 | Beneficios desconocidos conservan descripción original. | Persistencia verificada con Other y Unknown. |
| #10 HU10 | Fechas DATE / DateOnly, sin inventar vigencias. | Ida y vuelta MySQL/HTTP, límites inclusivos y zona argentina probados. |
| #11 HU11 | Importes decimales y moneda publicada. | Precisión persistida y leída vía HTTP; sin cálculos comparativos. |
| #12 HU12 | Cadenas consistentes y nombres originales conservados. | Registro de las tres cadenas/fuentes y mapeo público verificados. |
| #13 HU13 | Ausentes como null; código incierto distinto de ausencia confirmada. | HTTP y UI muestran estados diferenciados. |
| #14 HU14 | Índice único y actualización por identidad de extracción. | Relecturas sin duplicados, cambios a Pending y conservación de Disabled probados. |
| #15 HU15 | Solo Published dentro de fechas conocidas. | Predicado SQL y detalle oculto para estados/fechas no disponibles probados. |
| #16 HU16 | Revisión explícita si no se conoce vencimiento. | Aviso administrativo; no se inventa una fecha ni se publica automáticamente. |
| #17 HU17 | Login y sesión administrativa protegida. | Administrador con hash en MySQL, cookie, CSRF, permisos y logout probados. |
| #18 HU18 | Consultas administrativas de ofertas y detalle. | Endpoints y revisión en navegador reales, incluso no publicadas. |
| #19 HU19 | Registro de ejecución, etapa y fallos. | Fallos de extracción/OCR/cuota persistidos, sin secretos ni cuerpo privado del proveedor. |
| #20 HU20 | Autorización explícita para publicar. | POST protegido y publicación visible según vigencia, probados. |
| #21 HU21 | Desactivación protegida, sin reactivación automática. | Retiro del catálogo y preservación en relecturas probados. |
| #22 HU22 | Reprocesar fuente desde administración. | Endpoint protegido comparte coordinador; llamada externa real pendiente de claves. |
| #23 HU23 | Listado público sin registro. | HTTP y navegador contra datos temporales en MySQL, limpieza posterior. |
| #24 HU24 | Detalle con beneficios, condiciones, fechas y origen. | HTTP y navegador móvil/escritorio probados. |
| #25 HU25 | Código como texto seleccionable. | Texto preservado; no se agregó botón específico para copiar, excluido por el MVP. |
| #26 HU26 | Ausencia confirmada de código. | NotRequired no expone texto residual y muestra el mensaje requerido. |
| #27 HU27 | Acceso a publicación original. | Enlace HTTP/HTTPS y activación en navegador; no certifica carga del sitio ni canje. |
| #28 HU28 | Reporte anónimo con dos motivos. | HTTP y formulario real guardan reporte sin desactivar oferta. |
| #29 HU29 | Comentario opcional. | Vacío/null, contenido y límite de 2000 caracteres probados. |
| #30 HU30 | Consulta administrativa de reportes. | Protegida, enlaza oferta y funciona tras desactivación. |
| #31 HU31 | Consultas de detalles públicos exitosos. | Excluye listado, detalles fallidos y revisión administrativa. |
| #32 HU32 | Registro de activación de enlace de origen. | Evento independiente; envío no bloqueante, de mejor esfuerzo. |
| #33 HU33 | Registro mínimo de reportes recibidos. | Un registro por envío exitoso, sin contar envíos inválidos y sin identificar consumidores. |

## Decisiones acordadas

- Se usa el MVP adjunto, no el alcance del otro repositorio: no hay carga manual ni botón para copiar.
- Gemini Developer API es el proveedor activo, sin fallback ni servicios pagos necesarios; OCR.space se mantiene.
- Un modelo y una clave no prueban el nivel gratuito de un proyecto: corresponde elegir un proyecto Free tier sin facturación en AI Studio. El programa no activa facturación.
- El presupuesto Gemini se guarda en MySQL, cuenta intentos fallidos, se reinicia por día del Pacífico y respeta la pausa informada por el proveedor.
- Las cadenas/fuentes se configuran externamente y se sincronizan al migrar. La relación fuente/cadena es relacional; los identificadores históricos de fuente en ofertas/ejecuciones se conservan para que cambios de configuración no borren el origen.
- La ruta principal para Windows usa MySQL local, Visual Studio, User Secrets y scripts PowerShell. Docker/Python son una alternativa, no un requisito de esa ruta.
