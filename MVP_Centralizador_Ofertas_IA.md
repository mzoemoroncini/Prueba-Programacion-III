# MVP — Centralizador de ofertas y descuentos con IA

## Objetivo

Validar que un sistema pueda leer, extraer y actualizar automáticamente ofertas publicadas por McDonald’s, Burger King y Mostaza, reunirlas en un catálogo y permitir al consumidor acceder a su publicación de origen.

La lectura, extracción, carga y actualización serán automáticas. El administrador supervisará los resultados, sin transcribir las ofertas.

## Usuarios

- **Consumidor/Cliente:** consulta ofertas, obtiene los códigos disponibles, accede a la fuente y reporta información incorrecta.
- **Administrador:** supervisa el proceso automático, controla las publicaciones y revisa los reportes.

## Problema 1: leer las páginas y extraer ofertas

### 1. Lectura y actualización automáticas

El sistema consultará periódicamente las fuentes definidas de las tres cadenas y cargará o actualizará sus ofertas.

**Justificación:** es necesario demostrar que el catálogo puede obtenerse y mantenerse sin carga manual.

### 2. OCR y lectura automática de documentos

El sistema usará OCR para promociones publicadas solo como imágenes y leerá automáticamente documentos vinculados a las ofertas. El alcance se limita a las imágenes y documentos de las fuentes seleccionadas.

**Justificación:** la información necesaria puede no estar disponible como texto en la página.

### 3. Clasificación, resúmenes e interpretación con IA

La IA identificará qué publicaciones contienen promociones, redactará resúmenes e interpretará sus condiciones basándose en la fuente.

Los datos ausentes se mostrarán como **“No informado por la fuente”**. No se inventarán precios, fechas ni condiciones.

**Justificación:** permite transformar publicaciones de formatos diferentes en información comprensible y utilizable.

### 4. Extracción y estructuración de todos los tipos de beneficio

Cada oferta conservará el tipo de beneficio, su descripción y los valores y requisitos publicados. Se contemplan descuentos porcentuales, descuentos de monto fijo, precios promocionales, 2×1, combos y otros beneficios.

Cuando aparezca un tipo no reconocido, se conservará su descripción original. No se calculará ni comparará su conveniencia.

**Justificación:** evita excluir promociones por la forma en que expresan el beneficio.

### 5. Normalización de fechas, precios y nombres

Las fechas y los importes tendrán formatos uniformes. Los nombres de las cadenas serán consistentes y se conservarán los nombres de los productos sin asumir equivalencias.

**Justificación:** reduce errores de interpretación y permite aplicar correctamente las fechas de vigencia.

### 6. Actualización de registros y control de vigencia

Las nuevas lecturas actualizarán las ofertas existentes sin duplicarlas. Las ofertas con inicio futuro o vencimiento informado no aparecerán como disponibles.

Las ofertas sin fecha de finalización quedarán sujetas a revisión administrativa.

**Justificación:** permite mantener un catálogo coherente y evitar que ofertas con fechas conocidas se presenten fuera de vigencia.

### 7. Supervisión administrativa mínima

El administrador contará con inicio de sesión y un panel básico para:

- Consultar las ofertas obtenidas por la IA.
- Consultar fallos de lectura, extracción y actualización.
- Autorizar publicaciones y desactivar ofertas.
- Solicitar una nueva lectura de la fuente.
- Revisar ofertas sin vencimiento informado.
- Consultar los reportes asociados a las ofertas.

**Justificación:** permite controlar errores de la IA sin convertir el proceso en una carga manual.

## Problema 2: elegir una oferta y abrir la página de origen

### 1. Listado y detalle sin registro

El consumidor podrá consultar un listado de ofertas publicadas y abrir el detalle de cada una sin crear una cuenta.

El detalle mostrará:

- Cadena.
- Resumen de la promoción.
- Tipo y descripción del beneficio.
- Precio o valores del beneficio, cuando estén publicados.
- Código, cuando exista.
- Vigencia y condiciones publicadas.
- Enlace a la publicación de origen.

**Justificación:** es el mínimo necesario para entender una promoción antes de acceder a la fuente.

### 2. Visualización del código y acceso a la fuente

El código podrá seleccionarse y copiarse como texto. Si no existe, se mostrará **“Esta oferta no incluye código”**.

Cada oferta tendrá un enlace a su publicación de origen. La compra y el canje se realizarán fuera del sistema.

**Justificación:** completa el recorrido principal: consultar una oferta, obtener el código cuando corresponda y acceder a su origen.

### 3. Reportes de usuarios

Cada detalle permitirá enviar un reporte con uno de estos motivos:

- Oferta vencida.
- Datos incorrectos.

El consumidor podrá agregar un comentario opcional. El reporte quedará asociado a la oferta y será visible para el administrador.

Un reporte no desactivará automáticamente la oferta.

**Justificación:** permite detectar errores que el proceso automático no identifica.

### 4. Registro mínimo de uso

El sistema registrará consultas de ofertas, accesos a las fuentes y reportes recibidos. No se requiere un panel estadístico avanzado.

**Justificación:** permite observar si los usuarios realizan el recorrido principal y qué problemas encuentran. Los accesos no se interpretarán como canjes exitosos.

## Interfaz mínima

- **Listado público de ofertas.**
- **Detalle de oferta**, con código, condiciones, enlace y opción de reporte.
- **Inicio de sesión del administrador.**
- **Panel básico de administración**, con ofertas, fallos y reportes.

OCR, IA, normalización y actualización formarán parte de un único proceso automático; no serán módulos separados para el usuario.

## Fuera del alcance

- Delivery.
- Comparación de ofertas por marca, productos, precio, beneficio, vigencia o código.
- Filtros y buscador.
- Registro y cuentas de consumidores.
- Favoritos y recomendaciones personalizadas.
- Notificaciones.
- Geolocalización y consulta de stock.
- Compras o canjes dentro del sistema.
- Botón específico para copiar códigos.
- Estadísticas avanzadas.
- Actualización en tiempo real.
- Carga o transcripción manual de ofertas.

## Validación

El MVP permitirá evaluar:

1. Si la extracción y estructuración reflejan fielmente la información de las fuentes, incluidas imágenes y documentos.
2. Si las nuevas lecturas actualizan registros sin generar duplicados.
3. Si el control de fechas evita mostrar ofertas fuera de su vigencia conocida.
4. Si los consumidores consultan ofertas y acceden a sus publicaciones de origen.
5. Qué errores reportan los consumidores y cuánto esfuerzo requiere la supervisión administrativa.

## Resumen

El consumidor tendrá listado, detalle, acceso a la fuente y reporte. El administrador tendrá supervisión de ofertas, fallos y reportes. Un único proceso automático realizará la lectura, OCR, interpretación con IA, normalización, carga y actualización.
