## Purpose

Definir cómo y dónde se ejecutan las herramientas de ORUMI en el servidor propio del usuario de forma autónoma, segura y sin exponer datos ni servicios innecesarios.

## ADDED Requirements

### Requirement: Ejecución en el servidor propio y autonomía
Mira y las herramientas de ORUMI MUST ejecutarse en el servidor propio del usuario y MUST NOT depender de otro software del usuario en tiempo de ejecución; los únicos servicios externos permitidos MUST ser los del proveedor de IA configurado, y la única infraestructura compartida MUST ser la instancia de base de datos, usada con una base y un rol propios de ORUMI y sin tablas compartidas con otros proyectos.

#### Scenario: Sin otras dependencias
- **WHEN** se ejecuta Mira
- **THEN** funciona sin consultar ni escribir en otros sistemas del usuario

#### Scenario: Base de datos propia
- **WHEN** se inspecciona la instancia de base de datos compartida
- **THEN** los datos de ORUMI están en una base y con un rol exclusivos y ningún otro proyecto accede a ellos

### Requirement: Lote periódico y ejecución a petición
El sistema MUST ejecutar el lote de Mira de forma periódica (por defecto de noche) y MUST permitir lanzarlo a petición con un único comando, incluso si el planificador no funciona.

#### Scenario: Planificador caído
- **WHEN** el planificador no ha ejecutado el lote
- **THEN** el usuario puede lanzarlo manualmente y procesa las capturas pendientes

### Requirement: Una sola ejecución a la vez
El sistema MUST impedir que dos ejecuciones de Mira procesen capturas al mismo tiempo, de modo que una captura no se procese dos veces ni se escriban registros duplicados.

#### Scenario: Ejecución solapada
- **WHEN** se lanza una ejecución mientras otra sigue en curso
- **THEN** la segunda termina sin procesar nada y lo indica

#### Scenario: Ejecución interrumpida
- **WHEN** una ejecución termina de forma abrupta
- **THEN** la siguiente puede arrancar sin intervención manual

### Requirement: Datos y secretos fuera del repositorio
Los datos de `orumi/`, las claves de API y cualquier secreto MUST NOT estar en el repositorio de código ni en su remoto, y MUST tener permisos que solo permitan acceso al usuario que ejecuta ORUMI.

#### Scenario: Revisión del repositorio
- **WHEN** se revisa el contenido versionado
- **THEN** no aparecen capturas, registros, claves ni configuración con secretos

#### Scenario: Permisos
- **WHEN** se inspeccionan `orumi/` y el fichero de secretos en el servidor
- **THEN** solo el usuario de ORUMI puede leerlos

### Requirement: Sin exposición de red nueva
En el MVP ORUMI MUST NOT abrir puertos ni exponer servicios nuevos a internet; la interacción se hace por línea de comandos en el servidor.

#### Scenario: Superficie de red
- **WHEN** se despliega el MVP
- **THEN** no hay ningún servicio nuevo escuchando fuera del servidor

### Requirement: Registros técnicos sin datos personales
Los registros técnicos (logs) MUST NOT contener el texto de capturas, registros ni preguntas, solo identificadores, recuentos y errores.

#### Scenario: Error al procesar
- **WHEN** falla el procesamiento de una captura
- **THEN** el log indica el identificador y el motivo sin reproducir su contenido
