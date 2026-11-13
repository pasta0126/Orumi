## ADDED Requirements

### Requirement: Datos cifrados en reposo
Los datos de `orumi/` MUST almacenarse en un volumen cifrado en reposo, de modo que su contenido no sea legible sin la clave si se extrae el almacenamiento del servidor.

#### Scenario: Almacenamiento extraído
- **WHEN** se lee el almacenamiento del servidor sin la clave de descifrado
- **THEN** el contenido de `orumi/` no es legible

### Requirement: Fallo cerrado si el volumen no está disponible
Las herramientas MUST NOT escribir ni procesar datos si el volumen cifrado no está montado y disponible, y MUST NOT crear ficheros de datos en el punto de montaje vacío.

#### Scenario: Arranque sin desbloquear
- **WHEN** el servidor se reinicia y el volumen cifrado aún no se ha desbloqueado
- **THEN** el lote periódico no procesa nada ni escribe ficheros, deja las capturas pendientes y lo registra

#### Scenario: Captura con el volumen bloqueado
- **WHEN** se intenta crear una captura con el volumen bloqueado
- **THEN** se rechaza con un mensaje claro y no se crea ningún fichero

#### Scenario: Desbloqueo posterior
- **WHEN** el usuario desbloquea el volumen
- **THEN** el siguiente lote procesa las capturas que quedaron pendientes

### Requirement: Recuperación de la clave
La clave o frase de descifrado MUST estar recuperable por el usuario fuera del servidor, porque su pérdida implica la pérdida irreversible de los datos al no existir respaldo.

#### Scenario: Pérdida de acceso al servidor
- **WHEN** el usuario necesita descifrar el volumen tras un fallo
- **THEN** dispone de la clave guardada fuera del servidor
