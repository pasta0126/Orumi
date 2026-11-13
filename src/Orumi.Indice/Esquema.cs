using System.Text.RegularExpressions;

namespace Orumi.Indice;

/// <summary>Esquema lógico del índice derivado (<c>docs/indice-derivado.md</c> §1). Es desechable: se regenera desde los ficheros.</summary>
public static partial class Esquema
{
    public const string Vivo = "indice";

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")] private static partial Regex IdentificadorRx();

    /// <summary>Los nombres de esquema se interpolan en SQL: solo se admiten identificadores simples.</summary>
    public static string Validar(string nombre) =>
        IdentificadorRx().IsMatch(nombre) ? nombre : throw new ArgumentException($"Nombre de esquema no válido: {nombre}");

    public static string Crear(string esquema)
    {
        var s = Validar(esquema);
        return $"""
            CREATE SCHEMA {s};

            CREATE TABLE {s}.meta (clave text PRIMARY KEY, valor text NOT NULL);

            CREATE TABLE {s}.captura (
              id text PRIMARY KEY, ruta text NOT NULL, comunicada_el timestamptz, origen text,
              fecha_acontecimiento date, etiqueta text, dispositivo text, huella_ok boolean NOT NULL);

            CREATE TABLE {s}.registro (
              id text PRIMARY KEY, ruta text NOT NULL, clase text, titulo text, comunicada_el timestamptz,
              fecha_expresion text, fecha_desde date, fecha_hasta date,
              hora_expresion text, hora_valor time, hora_aproximada boolean,
              duracion_expresion text, duracion_minutos integer,
              multidia_estado text, multidia_fin_desde date, multidia_fin_hasta date,
              clave_extraccion text, version_agente text, modelo text, generado_el timestamptz, actualizado_el timestamptz);

            CREATE TABLE {s}.registro_dato (
              registro_id text NOT NULL, campo text NOT NULL, valor text NOT NULL, valor_normalizado text NOT NULL, certeza text);

            CREATE TABLE {s}.registro_entidad (
              registro_id text NOT NULL, nombre text NOT NULL, nombre_normalizado text NOT NULL, tipo text, certeza text);

            CREATE TABLE {s}.registro_certeza (registro_id text NOT NULL, campo text NOT NULL, certeza text NOT NULL);

            CREATE TABLE {s}.fuente (
              registro_id text NOT NULL, captura_id text NOT NULL, rol text, fragmento_desde integer, fragmento_hasta integer);

            CREATE TABLE {s}.entrada (registro_id text NOT NULL, fecha date, captura_id text, resumen text);

            CREATE TABLE {s}.relacion (origen_id text NOT NULL, destino_id text NOT NULL, tipo text, certeza text, evidencia text);

            CREATE TABLE {s}.conflicto (
              registro_id text NOT NULL, con_registro_id text NOT NULL, campo text, pregunta_id text, estado text);

            CREATE TABLE {s}.rechazo (registro_id text NOT NULL, campo text, valor text, rechazada_el timestamptz);

            CREATE TABLE {s}.historial (
              registro_id text NOT NULL, orden integer NOT NULL, fecha timestamptz, origen text, campo text, pregunta_id text, captura_id text);

            CREATE TABLE {s}.pregunta (
              id text PRIMARY KEY, ruta text NOT NULL, registro_id text, campo text, texto text, estado text, valor text,
              creada_el timestamptz, cerrada_el timestamptz);

            CREATE TABLE {s}.lote (
              id text PRIMARY KEY, ejecutado_el timestamptz, ejecucion text, version_agente text, modelo text, proveedor text);

            CREATE TABLE {s}.lote_captura (lote_id text NOT NULL, captura_id text NOT NULL, registro_id text, accion text);

            CREATE INDEX ON {s}.registro (fecha_desde, fecha_hasta);
            CREATE INDEX ON {s}.registro (clase);
            CREATE INDEX ON {s}.registro_certeza (certeza);
            CREATE INDEX ON {s}.registro_dato (campo, valor_normalizado);
            CREATE INDEX ON {s}.registro_entidad (nombre_normalizado);
            CREATE INDEX ON {s}.pregunta (estado, valor);
            CREATE INDEX ON {s}.relacion (origen_id);
            CREATE INDEX ON {s}.relacion (destino_id);
            """;
    }
}
