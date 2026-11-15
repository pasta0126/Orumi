# ORUMI

Memoria personal estructurada: **escucha, comprende y conecta.** No es un diario, ni un gestor de tareas, ni un sistema de vigilancia. Lo que cuentas se conserva tal cual (el original es irremplazable) y se organiza en registros que tú controlas.

## Componentes

- **Captura** — por terminal (`orumi capturar`) o por web instalable (solo texto). Crea capturas inmutables.
- **Mira** — el agente de seguimiento: convierte las capturas en registros, pregunta lo que falta (también el contenido), repasa lo ya registrado, guarda el clima, contrasta tus hipótesis y aprende de cómo cierras sus preguntas. Ver `docs/agente-mira.md`.
- **Noa** — el agente del perfil: a partir de lo que has contado y con evidencia visible, te devuelve cómo pareces funcionar. Solo tuyo, sin diagnósticos, con control total. Ver `docs/agente-noa.md`.
- **Panel web** — ver por día, buscar, responder con un clic, corregir, fusionar, descubrimientos y perfil.

Los ficheros (`.md` y `.yaml`) son la fuente de verdad; el índice de PostgreSQL es derivado y regenerable. Stack: .NET, HTML/CSS/JS sin dependencias y proveedor de IA Anthropic.

## Metodología

Spec-Driven Development con OpenSpec: no se escribe código sin una change aprobada (`openspec/specs/`, `openspec/changes/`). Las decisiones están en `docs/decisiones.md`; las instrucciones para agentes, en `AGENTS.md`.

## Desarrollo

```bash
dotnet build
dotnet test
dotnet run --project src/Orumi.Cli -- --ayuda
```

Los detalles de infraestructura y despliegue son privados y viven en el repo `infra` del servidor.
