## Context

Cada registro tiene una clave `captura#f<n>:<clase>:<k>`; al reprocesar, Mira ofrece al modelo las claves existentes de esa captura y reconoce lo existente por clave o por fragmento y clase (`EsElMismo`). Un campo es «protegido» frente a Mira si tiene certeza `confirmado` o alguna entrada de historial de usuario. Las operaciones de corrección ya validan antes de escribir y escriben de forma atómica (`Operaciones`), y comparten bloqueo con Mira.

## Goals / Non-Goals

**Goals:** control total y reversible, con Mira respetando lo hecho y las capturas intactas.
**Non-Goals:** borrado definitivo, deshacer múltiple, edición de capturas.

## Decisions

1. **Tumbas en lugar de borrar.** Eliminar y fusionar no quitan el fichero: añaden `estado: eliminado` (con `eliminado_el`) o `estado: fusionado` (con `fusionado_en: <id destino>` y `fusionado_el`). Ausente `estado` = activo. Así la clave de extracción sigue ocupada (Mira nunca reutiliza una clave), restaurar es trivial y la trazabilidad se conserva. *Alternativa:* mover a una carpeta `papelera/`; descartada porque Mira necesita las claves y habría que cargar dos árboles.
2. **Mira y las tumbas.** Los registros no activos se siguen contando como «existentes de la captura» (para claves y coincidencia por fragmento y clase) pero **no se ofrecen** al modelo como claves reclamables ni como registros relacionados. Si el modelo propone algo que coincide con una tumba, esa acción se **omite** sin escribir nada (la acción lleva `Omitir`); las relaciones hacia un registro omitido se descartan. Para las separaciones, el registro original guarda `separados: [capturas]` y una ampliación de ese registro con una de ellas se omite.
3. **Deshacer con certeza anterior.** Las entradas de historial del usuario guardan ahora también `certeza_anterior` (la que tenía el campo antes del cambio). `deshacer` toma la última entrada de usuario del campo que no haya sido deshecha, restaura `anterior` y esa certeza (o la quita si el valor anterior era desconocido) y añade una entrada `{origen: usuario, campo, anterior: <valor deshecho>, nuevo: <valor restaurado>, deshace: <fecha de la entrada>}`. Para entradas antiguas sin `certeza_anterior`: si el valor anterior era desconocido se quita la certeza; si no, se restaura como `interpretado` (la más débil) y se avisa. Un rechazo también se deshace (el valor vuelve desde `rechazos`).
4. **Reglas del validador.** `estado` solo admite `eliminado` o `fusionado`; `fusionado` exige `fusionado_en` que exista, esté activo y no sea el mismo; la regla «campo corregido por el usuario con certeza confirmado» se aplica a la última entrada de usuario del campo salvo que sea una reversión (`deshace`). `EsProtegido` pasa a considerar la última entrada de usuario del campo, de modo que tras deshacer Mira vuelve a poder actualizarlo.
5. **Fusionar B en A.** Se añaden a A: las fuentes de B (con rol `ampliacion`), sus entradas, sus relaciones (reapuntando las que apuntaban a B hacia A y descartando las que apuntan a A) y los campos que A no tiene (con su certeza). En los campos donde ambos tienen valor y difieren, gana A y el valor de B se anota en el historial de A (`anterior`/`nuevo` invertidos con motivo «valor del registro fusionado»). Los registros que tenían conflictos o relaciones con B se reapuntan a A; los conflictos entre A y B se resuelven. B pasa a tumba. Las preguntas pendientes de B se cierran.
6. **Separar captura C de A.** Solo si C figura en `fuentes` de A con rol `ampliacion`. Se crea R con la fuente C como `origen` (con su fragmento si lo tenía), la entrada de C, clase y título de A (o `--titulo`), fecha tomada de la entrada de C si existe, el resto de campos desconocidos, una clave nueva `C#f<n+1>:<clase>:1` (el siguiente `n` libre de esa captura), y un historial. A pierde la fuente y la entrada de C, anota `separados: [C]` y su historial. Se devuelve la lista de entradas de historial de A que citaban a C para que el usuario revise los datos que aportó.
7. **Eliminar:** además de marcar la tumba, cierra las preguntas pendientes del registro (`no_relevante`, respuesta «Registro eliminado»), resuelve sus conflictos en los otros registros y retira las relaciones entrantes (se conservan en la tumba para restaurar). Restaurar quita `estado` y no reabre preguntas ya cerradas.
8. **Operación atómica multi-fichero:** las operaciones cambian varios ficheros; se prepara el almacén resultante en memoria, se valida y se escribe todo; si falla una escritura se relanza sin dejar el estado a medias (los reemplazos atómicos se hacen en orden y el validador al arrancar detecta cualquier incoherencia).
9. **Índice:** se añade `estado` a `registro` y las consultas filtran `estado = 'activo'`. El panel (`Lectura`) filtra en memoria, expone la papelera y permite abrir la ficha de un registro eliminado con un aviso y el botón de restaurar.
10. **Interfaces:** comandos `deshacer`, `eliminar`, `restaurar`, `fusionar` y `separar`; endpoints `POST` equivalentes en el panel (las mismas cuatro reglas de seguridad que las demás escrituras); la página añade acciones en la ficha, un selector de registro para fusionar y la vista Papelera.

## Risks / Trade-offs

- **Fusionar pierde información de un campo en conflicto** → no se pierde: queda en el historial.
- **Separar no revierte los datos que aportó la captura** → se listan para revisión; revertirlos automáticamente exigiría atribuir cada dato a cada captura.
- **Tumbas acumuladas** → son ficheros pequeños; una limpieza definitiva será una decisión aparte.
- **Reglas de protección más finas** → cubiertas con pruebas de reprocesado tras cada operación.
- **Operaciones sobre varios ficheros** → validación previa en memoria y escritura en orden; el validador detecta incoherencias.
