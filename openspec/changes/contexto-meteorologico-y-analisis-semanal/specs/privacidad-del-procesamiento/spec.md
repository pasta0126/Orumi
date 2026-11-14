## MODIFIED Requirements

### Requirement: Constancia de lo enviado
Cada lote MUST indicar a qué proveedor y modelo se enviaron los datos, y MUST NOT haber otro destino de datos del usuario salvo el servicio meteorológico, al que solo MAY enviarse coordenadas aproximadas y fechas.

#### Scenario: Revisar un lote
- **WHEN** el usuario consulta un lote
- **THEN** ve el proveedor y el modelo usados

## ADDED Requirements

### Requirement: Servicio meteorológico
Mira MUST enviar al servicio meteorológico únicamente coordenadas redondeadas y rangos de fechas, MUST NOT enviarle texto de capturas, registros, preguntas ni identidad alguna, y MUST usar solo un servicio sin clave personal ni cuenta. Las condiciones del servicio y la fecha de verificación MUST constar en la configuración.

#### Scenario: Consulta de clima
- **WHEN** Mira consulta el clima
- **THEN** la petición contiene solo coordenadas redondeadas, fechas y variables pedidas

#### Scenario: Servicio no admitido
- **WHEN** el servicio configurado exige una cuenta o una clave personal
- **THEN** Mira no lo consulta y avisa
