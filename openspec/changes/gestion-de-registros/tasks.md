## 1. Formato, validador y protección

- [x] 1.1 `estado` (`eliminado`/`fusionado`, `eliminado_el`, `fusionado_en`, `fusionado_el`) y `separados` en el formato canónico y el validador; `certeza_anterior` y `deshace` en el historial
- [x] 1.2 Registrar `certeza_anterior` en las correcciones y rechazos existentes; ajustar `EsProtegido` y la regla de «corregido por el usuario» a la última entrada del campo
- [x] 1.3 Pruebas del validador y de la protección

## 2. Operaciones

- [x] 2.1 Deshacer (correcciones, rechazos, entradas antiguas sin certeza anterior)
- [x] 2.2 Eliminar y restaurar (preguntas, conflictos y relaciones)
- [x] 2.3 Fusionar (fuentes, entradas, relaciones, campos, conflictos de valores, reapuntado)
- [x] 2.4 Separar (solo ampliaciones; clave nueva; lista de datos a revisar)
- [x] 2.5 Escritura multi-fichero validada previamente; pruebas de cada operación y de los casos de error

## 3. Mira respeta lo hecho

- [ ] 3.1 Tumbas como existentes para claves pero no ofrecidas al modelo ni como relacionados
- [ ] 3.2 Acción `Omitir` ante coincidencia con una tumba o ampliación de un registro del que se separó la captura; descartar relaciones hacia registros omitidos
- [ ] 3.3 Pruebas de reprocesado tras eliminar, fusionar y separar (sin recrear, reactivar ni reunir)

## 4. Índice y consultas

- [ ] 4.1 Columna `estado` y filtros en las consultas; pruebas con PostgreSQL en el servidor

## 5. Interfaces

- [ ] 5.1 Comandos `deshacer`, `eliminar`, `restaurar`, `fusionar` y `separar` con bloqueo frente a Mira
- [ ] 5.2 API del panel: operaciones, papelera y ficha de registro eliminado; filtros de lectura
- [ ] 5.3 Página: acciones en la ficha, selector para fusionar, vista Papelera
- [ ] 5.4 Pruebas de comandos, API y reglas de interfaz

## 6. Documentación y despliegue

- [ ] 6.1 Documentar (`formato-de-ficheros`, `agente-mira`, guía, X58) y actualizar `infra`
- [ ] 6.2 Reconstruir imágenes, desplegar y probar con datos ficticios (incluido reprocesado con Mira real)
