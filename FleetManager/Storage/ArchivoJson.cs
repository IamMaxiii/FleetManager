using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FleetManager.Storage;

/// <summary>
/// Lee y escribe un archivo JSON de forma segura.
///
/// Al guardar:
///   1. Escribe los datos en un archivo temporal (.tmp) y obliga a llevarlo al disco.
///   2. Sustituye el archivo de golpe con File.Replace, que deja el anterior como .bak.
///      Si la aplicación se cierra a mitad, el archivo bueno sigue intacto.
///   3. Hace una copia diaria en la carpeta "copias" (se conservan las 10 últimas).
///
/// Al leer, si el archivo falta o está dañado, prueba con el .bak y luego con las
/// copias diarias. Los archivos dañados se apartan con otro nombre, nunca se borran.
/// Si encuentra un archivo que no debe tocar (de una versión más nueva, o que no se
/// puede abrir), bloquea el guardado para no sobrescribirlo.
/// </summary>
public sealed class ArchivoJson<T> where T : class, new()
{
    public const int VersionActual = 1;

    public const int CopiasDiariasAConservar = 10;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        // Escribe los acentos tal cual (y no como códigos á) para que se pueda leer con el Bloc de notas.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    /// <summary>Sin sangrías ni saltos de línea, para archivos grandes como el mapa.</summary>
    private static readonly JsonSerializerOptions OpcionesCompactas = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private enum ResultadoIntento
    {
        Correcto,
        Danado,
        Inaccesible,
        VersionMasNueva
    }

    private readonly string rutaTemporal;
    private readonly string rutaCopiaAnterior;
    private readonly string carpetaCopias;
    private readonly string nombreBase;
    private readonly Registro registro;
    private readonly TimeProvider reloj;
    private readonly JsonSerializerOptions opcionesEscritura;
    private readonly bool copiasDiarias;

    /// <param name="compacto">Escribir sin sangrías (para archivos grandes que no se leen a mano).</param>
    /// <param name="copiasDiarias">Hacer copia diaria en la carpeta de copias (no tiene sentido para archivos numerosos como los recorridos).</param>
    public ArchivoJson(string ruta, string carpetaCopias, Registro registro, TimeProvider reloj, bool compacto = false, bool copiasDiarias = true)
    {
        opcionesEscritura = compacto ? OpcionesCompactas : Opciones;
        this.copiasDiarias = copiasDiarias;
        Ruta = ruta;
        rutaTemporal = ruta + ".tmp";
        rutaCopiaAnterior = ruta + ".bak";
        this.carpetaCopias = carpetaCopias;
        nombreBase = Path.GetFileNameWithoutExtension(ruta);
        this.registro = registro;
        this.reloj = reloj;
    }

    public string Ruta { get; }

    public string NombreArchivo => Path.GetFileName(Ruta);

    /// <summary>
    /// Activo si al leer se encontró un archivo que no se debe sobrescribir.
    /// Mientras lo esté, <see cref="Guardar"/> se niega a escribir.
    /// </summary>
    public bool GuardadoBloqueado { get; private set; }

    public ResultadoLectura<T> Leer()
    {
        GuardadoBloqueado = false;
        var problemas = new List<string>();

        // 1. Archivo principal. 2. Copia del guardado anterior (.bak).
        var candidatos = new[]
        {
            (Archivo: Ruta, Origen: OrigenDatos.ArchivoPrincipal, Descripcion: NombreArchivo),
            (Archivo: rutaCopiaAnterior, Origen: OrigenDatos.CopiaAnterior, Descripcion: "la copia del guardado anterior")
        };

        foreach (var candidato in candidatos)
        {
            if (!File.Exists(candidato.Archivo))
            {
                continue;
            }

            var resultado = IntentarLeer(candidato.Archivo, out T? datos, out string error);

            if (resultado == ResultadoIntento.Correcto)
            {
                string? aviso = candidato.Origen == OrigenDatos.ArchivoPrincipal
                    ? null
                    : MensajeRecuperacion(problemas, candidato.Descripcion);

                return new ResultadoLectura<T>(datos!, candidato.Origen, aviso);
            }

            if (resultado != ResultadoIntento.Danado)
            {
                // Versión más nueva o archivo inaccesible: no está dañado, así que no se toca.
                return Bloquear($"No se pudo usar {Path.GetFileName(candidato.Archivo)}: {error}.");
            }

            problemas.Add($"{Path.GetFileName(candidato.Archivo)} estaba dañado ({error})");

            if (!Apartar(candidato.Archivo))
            {
                return Bloquear($"{Path.GetFileName(candidato.Archivo)} está dañado y no se pudo apartar.");
            }
        }

        // 3. Copias diarias, de la más reciente a la más antigua.
        foreach (string copia in CopiasDiarias())
        {
            var resultado = IntentarLeer(copia, out T? datos, out string error);

            if (resultado == ResultadoIntento.Correcto)
            {
                return new ResultadoLectura<T>(
                    datos!,
                    OrigenDatos.CopiaDiaria,
                    MensajeRecuperacion(problemas, $"la copia diaria {Path.GetFileName(copia)}"));
            }

            problemas.Add($"la copia diaria {Path.GetFileName(copia)} tampoco se pudo leer ({error})");
        }

        if (problemas.Count == 0)
        {
            // Primera vez: no hay nada guardado todavía.
            return new ResultadoLectura<T>(new T(), OrigenDatos.Nuevo, null);
        }

        return new ResultadoLectura<T>(
            new T(),
            OrigenDatos.Nuevo,
            $"No se pudieron recuperar los datos de {NombreArchivo}: {string.Join("; ", problemas)}. " +
            "Se empieza con datos vacíos. Los archivos dañados se han conservado en la carpeta de datos " +
            "con \"danado\" en el nombre.");
    }

    public void Guardar(T datos)
    {
        if (GuardadoBloqueado)
        {
            throw new InvalidOperationException(
                $"El guardado de {NombreArchivo} está bloqueado para no estropear el archivo existente.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);

        var documento = new DocumentoJson<T>
        {
            Version = VersionActual,
            GuardadoEn = reloj.GetUtcNow(),
            Datos = datos
        };

        byte[] contenido = JsonSerializer.SerializeToUtf8Bytes(documento, opcionesEscritura);

        // 1. Escribir el temporal y obligar a Windows a llevarlo al disco (no dejarlo en memoria).
        using (var flujo = new FileStream(
                   rutaTemporal, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            flujo.Write(contenido);
            flujo.Flush(flushToDisk: true);
        }

        // 2. Sustituir el archivo de golpe; el anterior queda como .bak.
        if (File.Exists(Ruta))
        {
            File.Replace(rutaTemporal, Ruta, rutaCopiaAnterior, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(rutaTemporal, Ruta);
        }

        // 3. Copia diaria. Si falla, el guardado ya está hecho: solo se anota.
        if (!copiasDiarias)
        {
            return;
        }

        try
        {
            HacerCopiaDiaria();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            registro.Aviso($"No se pudo hacer la copia diaria de {NombreArchivo}", ex);
        }
    }

    /// <summary>
    /// Guarda una copia aparte de <paramref name="datos"/> en la carpeta de copias (por
    /// ejemplo, antes de borrar algo). No cuenta como copia diaria y nunca se borra sola.
    /// </summary>
    /// <returns>Ruta de la copia.</returns>
    public string GuardarCopia(T datos, string motivo)
    {
        Directory.CreateDirectory(carpetaCopias);

        string marcaTiempo = reloj.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string ruta = Path.Combine(carpetaCopias, $"{nombreBase}-{motivo}-{marcaTiempo}.json");

        var documento = new DocumentoJson<T>
        {
            Version = VersionActual,
            GuardadoEn = reloj.GetUtcNow(),
            Datos = datos
        };

        File.WriteAllBytes(ruta, JsonSerializer.SerializeToUtf8Bytes(documento, opcionesEscritura));
        return ruta;
    }

    private ResultadoIntento IntentarLeer(string archivo, out T? datos, out string error)
    {
        datos = null;
        error = "";
        DocumentoJson<T>? documento;

        try
        {
            byte[] contenido = File.ReadAllBytes(archivo);
            documento = JsonSerializer.Deserialize<DocumentoJson<T>>(contenido, Opciones);
        }
        catch (JsonException ex)
        {
            error = "su contenido no es válido";
            registro.Error($"No se pudo interpretar {archivo}", ex);
            return ResultadoIntento.Danado;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"no se pudo abrir ({ex.Message})";
            registro.Error($"No se pudo abrir {archivo}", ex);
            return ResultadoIntento.Inaccesible;
        }

        if (documento?.Datos is null)
        {
            error = "está vacío o incompleto";
            registro.Error($"{archivo} está vacío o incompleto");
            return ResultadoIntento.Danado;
        }

        if (documento.Version > VersionActual)
        {
            error = $"lo creó una versión más nueva de FleetManager (formato {documento.Version})";
            registro.Error($"{archivo} tiene un formato más nuevo ({documento.Version}) que el que entiende esta versión ({VersionActual})");
            return ResultadoIntento.VersionMasNueva;
        }

        datos = documento.Datos;
        return ResultadoIntento.Correcto;
    }

    private ResultadoLectura<T> Bloquear(string motivo)
    {
        GuardadoBloqueado = true;

        string aviso = $"{motivo} Para no estropearlo, FleetManager no guardará cambios en {NombreArchivo} " +
                       "hasta que se resuelva y se vuelva a abrir la aplicación.";

        registro.Error(aviso);
        return new ResultadoLectura<T>(new T(), OrigenDatos.Nuevo, aviso);
    }

    private string MensajeRecuperacion(List<string> problemas, string fuente)
    {
        if (problemas.Count == 0)
        {
            return $"No se encontró {NombreArchivo}. Se han recuperado los datos desde {fuente}.";
        }

        return $"{string.Join("; ", problemas)}. Se han recuperado los datos desde {fuente}; " +
               "los cambios posteriores a esa copia se han perdido. Los archivos dañados se han " +
               "conservado en la carpeta de datos con \"danado\" en el nombre.";
    }

    /// <summary>Renombra un archivo dañado para conservarlo sin que estorbe. Nunca lo borra.</summary>
    private bool Apartar(string archivo)
    {
        string sufijo = archivo == Ruta ? "principal" : "anterior";
        string marcaTiempo = reloj.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string destino = Path.Combine(
            Path.GetDirectoryName(Ruta)!,
            $"{nombreBase}-danado-{marcaTiempo}-{sufijo}.json");

        try
        {
            File.Move(archivo, destino);
            registro.Aviso($"Archivo dañado apartado: {archivo} -> {destino}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            registro.Error($"No se pudo apartar el archivo dañado {archivo}", ex);
            return false;
        }
    }

    private void HacerCopiaDiaria()
    {
        string hoy = reloj.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string copia = Path.Combine(carpetaCopias, $"{nombreBase}-{hoy}.json");

        if (File.Exists(copia))
        {
            return;
        }

        Directory.CreateDirectory(carpetaCopias);
        File.Copy(Ruta, copia);

        foreach (string antigua in CopiasDiarias().Skip(CopiasDiariasAConservar))
        {
            File.Delete(antigua);
        }
    }

    /// <summary>Copias diarias de este archivo, de la más reciente a la más antigua.</summary>
    private IEnumerable<string> CopiasDiarias()
    {
        if (!Directory.Exists(carpetaCopias))
        {
            return [];
        }

        return Directory
            .GetFiles(carpetaCopias, nombreBase + "-*.json")
            .Where(EsCopiaDiaria)
            .OrderByDescending(ruta => ruta, StringComparer.Ordinal)
            .ToList();
    }

    private bool EsCopiaDiaria(string ruta)
    {
        string nombre = Path.GetFileNameWithoutExtension(ruta);

        return nombre.Length == nombreBase.Length + 11
               && DateOnly.TryParseExact(
                   nombre[(nombreBase.Length + 1)..],
                   "yyyy-MM-dd",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out _);
    }
}
