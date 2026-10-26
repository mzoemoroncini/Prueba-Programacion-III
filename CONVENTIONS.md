# CONVENTIONS.md

## Alcance del proyecto

Este proyecto centraliza ofertas de McDonald’s, Burger King y Mostaza.

- Frontend: React con JavaScript.
- Backend: C# y .NET.
- Base de datos: MySQL.
- Arquitectura: monolito modular.
- Usuarios: Consumidor/Cliente y Administrador.
- Lectura, extracción, carga y actualización de ofertas automáticas mediante IA, OCR y lectura de documentos.

Al generar funcionalidades, respetar el MVP y los criterios de aceptación del issue correspondiente. No agregar delivery, comparación de ofertas, cuentas de consumidores, filtros, favoritos, notificaciones, compras ni canjes dentro del sistema salvo una ampliación explícita del alcance.

## Organización del código

- Mantener el frontend en `/frontend` y el backend en `/backend`.
- Seguir la estructura existente antes de introducir una nueva.
- Agrupar código por capacidad funcional cuando ayude a mantenerlo.
- No crear una carpeta por historia o vertical slice.
- No crear módulos, abstracciones ni carpetas vacías anticipadamente.
- Extraer código compartido cuando exista reutilización real.
- Respetar los límites funcionales: acceder a datos de otra capacidad mediante sus operaciones definidas.

Los vertical slices representan flujos completos de negocio. Pueden atravesar frontend, backend y persistencia sin coincidir con carpetas o módulos.

## Nombres y estilo

- Escribir identificadores de código en inglés.
- Escribir textos de interfaz y documentación en español.
- Usar `PascalCase` para componentes React y clases C#.
- Usar `camelCase` para variables y funciones JavaScript.
- Usar las convenciones habituales de C#; los métodos asíncronos terminan en `Async`.
- Mantener los conceptos `Offer` y `OfferReport` de forma consistente.
- Conservar la convención de nombres de tablas adoptada por el proyecto.
- Respetar el estilo del código cercano y evitar cambios de formato ajenos a la tarea.

## Responsabilidades

- React presenta información y solicita acciones al backend.
- El backend controla permisos, reglas de negocio, persistencia y acceso a proveedores de IA.
- El frontend no accede directamente a MySQL ni utiliza credenciales de proveedores de IA.
- La lectura periódica y el reprocesamiento solicitado por el administrador reutilizan el mismo proceso.
- La validación del frontend mejora la experiencia; el backend valida las entradas y aplica las reglas.

## API y representación de datos

- Utilizar rutas claras bajo `/api`, con recursos en plural.
- Utilizar `GET` para consultas y `POST` para creación o acciones. Elegir otros métodos cuando expresen mejor la operación.
- Utilizar propiedades JSON en `camelCase`.
- No exponer directamente entidades de persistencia. Crear solo los DTO necesarios.
- Representar fechas sin hora como `YYYY-MM-DD`.
- Representar instantes en UTC mediante ISO 8601.
- Utilizar tipos decimales para importes.
- Representar datos desconocidos como `null`, sin sustituirlos por cero ni valores ficticios.
- Aplicar el formato visual en el frontend y las reglas de vigencia en el backend.

## Reglas de ofertas y publicaciones

- Los estados de publicación son `Pending`, `Published` y `Disabled`.
- Una oferta nueva queda pendiente de autorización administrativa.
- Una oferta disponible debe estar publicada y cumplir las fechas conocidas.
- Las ofertas con inicio futuro o finalización vencida no se presentan como disponibles.
- Las fechas de vigencia sin hora se evalúan según la hora de Argentina, incluyendo el día de finalización.
- Las ofertas sin fecha de finalización requieren revisión administrativa; no se supone vigencia indefinida.
- Toda oferta publicable tiene una cadena y un enlace de origen.
- Las nuevas lecturas actualizan registros existentes sin duplicarlos.
- Una nueva lectura no reactiva una oferta desactivada.
- Un fallo de lectura no elimina la información previamente guardada.

## Extracción y uso de IA

- Extraer información únicamente de las fuentes definidas y sus imágenes o documentos vinculados.
- No inventar precios, fechas, códigos, productos ni condiciones.
- Los resúmenes y las condiciones interpretadas deben conservar el significado de la fuente.
- Normalizar formatos sin alterar los valores publicados.
- Conservar los nombres originales de productos sin asumir equivalencias.
- Conservar la descripción original de beneficios no reconocidos.
- No convertir una extracción fallida o incierta en una ausencia confirmada.
- Mostrar `No informado por la fuente` para datos ausentes.
- Mostrar `Esta oferta no incluye código` cuando corresponda.
- Mantener el código como texto seleccionable, conservando sus caracteres.

## Reportes y medición

- Los consumidores pueden reportar ofertas sin registrarse.
- Los motivos son `Oferta vencida` y `Datos incorrectos`.
- El comentario es opcional.
- Cada reporte conserva su oferta, motivo, fecha y comentario cuando exista.
- Un reporte no desactiva automáticamente la oferta.
- Registrar consultas públicas, activaciones del enlace de origen y reportes recibidos.
- No contar consultas administrativas como consultas públicas.
- Un clic hacia la fuente no confirma la carga de la página ni un canje exitoso.
- No agregar un panel estadístico avanzado al MVP.

## Acceso y configuración

- Proteger las consultas y acciones administrativas en el backend.
- No depender únicamente de ocultar controles en el frontend.
- Mantener el catálogo público accesible sin registro.
- No guardar secretos en el repositorio.
- Utilizar configuración externa para credenciales, claves de IA y conexión a MySQL.
- Documentar las variables necesarias con ejemplos sin credenciales reales.
- Versionar los cambios de estructura de MySQL mediante migraciones.

## Errores

- Registrar fallos del proceso automático con fecha, fuente, etapa y descripción.
- Mostrar mensajes comprensibles al usuario.
- No exponer credenciales ni detalles internos en respuestas públicas.
- Distinguir una fuente sin ofertas de una lectura o extracción fallida.

## Desarrollo y verificación

- Implementar el flujo necesario para cumplir los criterios del issue.
- Evitar refactorizaciones y dependencias ajenas a la tarea.
- Vincular los cambios con sus issues y describir claramente su propósito.
- Un cambio puede cubrir varias historias o slices cuando complete un flujo coherente.
- Verificar los criterios de aceptación correspondientes.
- Automatizar pruebas de reglas críticas: permisos, vigencia, duplicados y estados.
- No exigir una prueba por pantalla o campo.
- Indicar qué se verificó y qué limitaciones quedan pendientes.
- No realizar commits, pushes ni cambios de alcance sin autorización explícita.

## Documentación

Mantener un README con instrucciones para:

- Ejecutar frontend y backend.
- Configurar MySQL y las variables necesarias.
- Aplicar las migraciones.
- Ejecutar las verificaciones disponibles.

Actualizar la documentación cuando un cambio modifique esas instrucciones.
