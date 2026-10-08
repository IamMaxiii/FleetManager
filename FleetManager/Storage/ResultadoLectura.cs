namespace FleetManager.Storage;

/// <summary>
/// De dónde salieron los datos al leer un archivo.
/// </summary>
public enum OrigenDatos
{
    /// <summary>Del archivo normal (por ejemplo, historial.json).</summary>
    ArchivoPrincipal,

    /// <summary>De la copia del guardado anterior (.bak), porque el principal faltaba o estaba dañado.</summary>
    CopiaAnterior,

    /// <summary>De una copia diaria de la carpeta "copias".</summary>
    CopiaDiaria,

    /// <summary>No había datos que leer: se empieza de cero.</summary>
    Nuevo
}

/// <param name="Datos">Los datos leídos (o vacíos si no había nada recuperable).</param>
/// <param name="Origen">De dónde salieron.</param>
/// <param name="Aviso">Mensaje para el usuario si hubo algún problema; vacío si todo fue bien.</param>
public sealed record ResultadoLectura<T>(T Datos, OrigenDatos Origen, string? Aviso);
