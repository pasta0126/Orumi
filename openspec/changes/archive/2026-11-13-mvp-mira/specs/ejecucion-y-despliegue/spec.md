## ADDED Requirements

### Requirement: Cifrado en reposo opcional
Los datos de `orumi/` MAY almacenarse en un volumen cifrado, pero el cifrado en reposo MUST NOT ser obligatorio: por decisión explícita del propietario, por defecto los datos se guardan sin cifrar en un directorio con permisos restringidos.

#### Scenario: Instalación por defecto
- **WHEN** se despliega ORUMI sin activar la exigencia de volumen
- **THEN** los datos se guardan en un directorio de acceso restringido al usuario, sin cifrado en reposo

### Requirement: Fallo cerrado cuando se exige el volumen
Si se activa la exigencia de volumen cifrado, las herramientas MUST NOT escribir ni procesar datos mientras el volumen no esté disponible, y MUST NOT crear ficheros de datos en el punto de montaje vacío.

#### Scenario: Volumen exigido y bloqueado
- **WHEN** la exigencia está activa y el volumen no está desbloqueado
- **THEN** ninguna herramienta escribe ni procesa, las capturas quedan pendientes y se informa con un mensaje claro

#### Scenario: Desbloqueo posterior
- **WHEN** el usuario desbloquea el volumen
- **THEN** el siguiente lote procesa las capturas que quedaron pendientes
