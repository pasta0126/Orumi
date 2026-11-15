# privacidad-del-procesamiento Specification

## Purpose
Garantizar que los datos personales del usuario solo salgan hacia el proveedor de IA lo estrictamente necesario y con garantías de no reutilización, y que Mira falle sin enviar nada si esas garantías no existen.

## Requirements

### Requirement: Mínimo necesario
Mira MUST enviar al proveedor de IA únicamente el texto de la captura y, cuando sea necesario para reclamar claves o detectar coincidencias, los datos mínimos de los registros relacionados, y MUST NOT enviar audio, ubicación ni ficheros ajenos a ORUMI.

#### Scenario: Procesar una captura
- **WHEN** Mira procesa una captura
- **THEN** solo salen el texto de esa captura y los registros relacionados necesarios

#### Scenario: Contenido ajeno
- **WHEN** existen otros ficheros del usuario fuera de `orumi/`
- **THEN** no se envían

### Requirement: Garantías del proveedor
Mira MUST usar únicamente proveedores cuyas condiciones impidan emplear los datos del usuario para entrenar o mejorar sus productos, y MUST NOT procesar ninguna captura si no puede asegurarlo.

#### Scenario: Proveedor sin garantías
- **WHEN** el servicio configurado no garantiza que los datos no se reutilicen
- **THEN** Mira no envía nada, deja las capturas pendientes y avisa en el lote

### Requirement: Constancia de lo enviado
Cada lote MUST indicar a qué proveedor y modelo se enviaron los datos, y MUST NOT haber otro destino de datos del usuario salvo el servicio meteorológico, al que solo MAY enviarse coordenadas aproximadas y fechas y, para ubicar una ciudad que no se conoce, su nombre.

#### Scenario: Revisar un lote
- **WHEN** el usuario consulta un lote
- **THEN** ve el proveedor y el modelo usados

### Requirement: Servicio meteorológico
Mira MUST enviar al servicio meteorológico únicamente coordenadas redondeadas y rangos de fechas y, para ubicar una ciudad que no conoce, el nombre que el usuario escribió (nada más), MUST NOT enviarle texto de capturas, registros, preguntas ni identidad alguna, y MUST usar solo un servicio sin clave personal ni cuenta. Las condiciones del servicio y la fecha de verificación MUST constar en la configuración.

#### Scenario: Consulta de clima
- **WHEN** Mira consulta el clima
- **THEN** la petición contiene solo coordenadas redondeadas, fechas y variables pedidas

#### Scenario: Ubicar una ciudad
- **WHEN** Mira ubica una ciudad que no conoce
- **THEN** la petición contiene solo el nombre de la ciudad y los parámetros de formato

#### Scenario: Servicio no admitido
- **WHEN** el servicio configurado exige una cuenta o una clave personal
- **THEN** Mira no lo consulta y avisa
