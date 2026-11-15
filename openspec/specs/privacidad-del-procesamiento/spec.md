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

### Requirement: Envío mínimo de Noa
Noa MUST enviar al proveedor de IA únicamente los datos agregados mínimos necesarios para el apartado que se genera, MUST usar solo el proveedor y las garantías de no reutilización ya exigidos a Mira, MUST NOT enviar audio, ubicación ni datos de otros destinos, MUST NOT conservar copia de lo enviado fuera de los ficheros del perfil, y MUST dejar constancia, también en las ejecuciones periódicas, en cada generación del proveedor y el modelo usados. Si no puede asegurar las garantías, MUST NOT generar nada y MUST avisar.

#### Scenario: Generar un apartado
- **WHEN** Noa genera un apartado del perfil
- **THEN** solo salen los extractos agregados necesarios y la generación registra proveedor y modelo

#### Scenario: Proveedor sin garantías
- **WHEN** no se puede asegurar la no reutilización de los datos
- **THEN** no se envía nada y se avisa al usuario

### Requirement: El repaso se somete a las mismas garantías
La capa del repaso que usa el modelo MUST enviar solo los registros con huecos y el texto mínimo necesario para formular la pregunta, MUST usar únicamente el proveedor y las garantías ya exigidas a Mira, MUST NOT enviar clima, audio, ubicación ni datos de otros destinos y MUST dejar constancia del proveedor y del modelo. Si no puede asegurar las garantías, MUST NOT enviar nada y MUST mantener solo la capa determinista, avisando.

#### Scenario: Repaso con garantías vigentes
- **WHEN** las garantías están vigentes
- **THEN** solo salen los registros con huecos y el repaso registra proveedor y modelo

#### Scenario: Sin garantías
- **WHEN** la garantía de privacidad no está vigente
- **THEN** no se envía nada al proveedor y se ejecuta solo la capa determinista
