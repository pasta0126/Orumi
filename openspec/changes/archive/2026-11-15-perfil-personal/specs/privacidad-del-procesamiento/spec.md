## ADDED Requirements

### Requirement: Envío mínimo de Noa
Noa MUST enviar al proveedor de IA únicamente los datos agregados mínimos necesarios para el apartado que se genera, MUST usar solo el proveedor y las garantías de no reutilización ya exigidos a Mira, MUST NOT enviar audio, ubicación ni datos de otros destinos, MUST NOT conservar copia de lo enviado fuera de los ficheros del perfil, y MUST dejar constancia, también en las ejecuciones periódicas, en cada generación del proveedor y el modelo usados. Si no puede asegurar las garantías, MUST NOT generar nada y MUST avisar.

#### Scenario: Generar un apartado
- **WHEN** Noa genera un apartado del perfil
- **THEN** solo salen los extractos agregados necesarios y la generación registra proveedor y modelo

#### Scenario: Proveedor sin garantías
- **WHEN** no se puede asegurar la no reutilización de los datos
- **THEN** no se envía nada y se avisa al usuario
