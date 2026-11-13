## 1. Formato

- [x] 1.1 Documentar en `docs/formato-de-ficheros.md` el campo `entidades` (`nombre`, `tipo` opcional, certeza) y su criterio frente a `herramientas`
- [x] 1.2 Documentar el campo `hora` (`expresion`, `valor`, `aproximada`), su certeza propia y la regla de no convertir franjas en horas
- [x] 1.3 Indicar en el formato que ambos campos son opcionales y que los registros existentes siguen siendo válidos
- [x] 1.4 Documentar el estado de pregunta `no_relevante` y el criterio de qué se pregunta (X36)

## 2. Índice

- [x] 2.1 Añadir a `docs/indice-derivado.md` la entidad `registro_entidad` y las columnas de hora, y las consultas por entidad y hora
- [x] 2.2 Documentar la comparación de entidades sin distinguir mayúsculas ni acentos

## 3. Ejemplos y validación

- [x] 3.1 Actualizar los ejemplos de `docs/ejemplos/` con entidades y hora (hora aproximada, franja sin hora, certezas distintas, nombre corregido)
- [x] 3.2 Actualizar el `README` de los ejemplos con los escenarios nuevos
- [x] 3.3 Revisar los ejemplos contra los escenarios de las specs con el script de validación, ampliado a los campos nuevos
- [x] 3.4 (Opcional) Actualizar a mano los datos de prueba locales de `data/orumi/` con los campos nuevos y revalidarlos

## 4. Cierre

- [x] 4.1 Actualizar `docs/decisiones.md` con las decisiones cerradas
