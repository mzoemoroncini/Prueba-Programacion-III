# Modelo inicial de ofertas

Offer pertenece a gestión de publicaciones. Captación entregará información mediante las operaciones que se implementen después, sin cambiar directamente el estado de publicación.

Una oferta puede contener varios beneficios. Cada beneficio conserva la descripción original, valores y requisitos publicados. Unknown significa tipo indeterminado; Other representa un beneficio identificado fuera de las categorías conocidas. Los nombres de productos se conservan en la descripción original por ahora.

CodeStatus distingue código disponible, ausencia confirmada y extracción incierta. Solo NotRequired permite mostrar «Esta oferta no incluye código». Available requiere un código no vacío.

Las fechas desconocidas son nulas. DateOnly representa fechas completas; DateTimeOffset representa instantes UTC. Moneda y valores solo se completan con evidencia de la fuente.

## Mapeo previsto a MySQL

- Tabla de ofertas y tabla de beneficios relacionadas por OfferId.
- GUID como char(36).
- Fechas de vigencia como date; instantes UTC como datetime(6).
- Importes decimal(18,2) y porcentajes decimal(7,4).
- Descripciones y condiciones longtext; cadenas limitadas según las anotaciones.
- Enumeraciones como enteros con valores explícitos.
- Índices de estado y vigencia, y referencia de origen.

El enlace de origen no es único: una página puede contener varias promociones. SourceOfferId es opcional y solo se registra si la fuente ofrece un identificador estable. La identificación de ofertas sin ese identificador se definirá al implementar cada extracción.

El proveedor MySql.EntityFrameworkCore, el contexto y la migración InitialOffers están implementados. ExtractionKey tiene un índice único; se calcula con la fuente configurada y el identificador publicado o el título original. CaptureSourceId vincula la oferta con su lector configurado. La migración se aplicó a la instancia MySQL local de desarrollo en este entorno. La publicación y ExtractedOfferWriter validan cadena, URL, fechas, código y valores numéricos. Las anotaciones por sí solas no ejecutan validaciones. Las lecturas inciertas no escriben; las modificaciones publicadas vuelven a revisión y las desactivadas conservan su estado.
