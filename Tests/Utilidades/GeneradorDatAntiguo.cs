using System.Globalization;
using System.Text;

namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Crea archivos con el formato de la versión antigua de FleetManager
/// (driving_sessions.dat V1/V2/V3 y driver_profile.dat) para probar el importador.
/// Escribe un valor por línea, igual que hacía la versión antigua.
/// </summary>
public sealed class GeneradorDatAntiguo
{
    private readonly List<string> lineas = [];
    private readonly int version;

    private GeneradorDatAntiguo(int version)
    {
        this.version = version;
    }

    /// <summary>Empieza un archivo de jornadas con la cabecera y el número de jornadas.</summary>
    public static GeneradorDatAntiguo Sesiones(int version, int numeroJornadas)
    {
        var generador = new GeneradorDatAntiguo(version);
        generador.lineas.Add($"FLEETMANAGER_SESSIONS_V{version}");
        generador.lineas.Add(Numero(numeroJornadas));
        return generador;
    }

    /// <summary>Contenido de un driver_profile.dat.</summary>
    public static string Perfil(string nombre, string apellidos, string fechaNacimiento, string permiso, string adr, double kilometros) =>
        string.Join(
            "\r\n",
            "FLEETMANAGER_DRIVER_PROFILE_V1",
            Texto(nombre),
            Texto(apellidos),
            Texto(fechaNacimiento),
            Texto(permiso),
            Texto(adr),
            Numero(kilometros)) + "\r\n";

    /// <summary>Cabecera de una jornada. En V1 va seguida directamente de <see cref="NumeroTrayectos"/>.</summary>
    public GeneradorDatAntiguo Jornada(Guid id, int numero, DateTime? inicio, DateTime? fin, bool abierta)
    {
        lineas.Add(id.ToString());
        lineas.Add(Numero(numero));
        lineas.Add(inicio.HasValue ? Numero(inicio.Value.Ticks) : "0");
        lineas.Add(fin.HasValue ? Numero(fin.Value.Ticks) : "-1");
        lineas.Add(abierta ? "1" : "0");
        return this;
    }

    /// <summary>Resumen por días (solo existe en V2 y V3).</summary>
    public GeneradorDatAntiguo Dias(params (DateTime Fecha, double Kilometros, int Trayectos)[] dias)
    {
        lineas.Add(Numero(dias.Length));

        foreach (var dia in dias)
        {
            lineas.Add(Numero(dia.Fecha.Date.Ticks));
            lineas.Add(Numero(dia.Fecha.Ticks));
            lineas.Add("-1");
            lineas.Add(Numero(dia.Kilometros));
            lineas.Add(Numero(dia.Trayectos));
        }

        return this;
    }

    public GeneradorDatAntiguo NumeroTrayectos(int cantidad)
    {
        lineas.Add(Numero(cantidad));
        return this;
    }

    public GeneradorDatAntiguo Trayecto(
        string origen,
        string destino,
        string carga,
        double kilometros,
        double media,
        double maxima,
        DateTime inicio,
        DateTime fin,
        bool faltaVelocidad = false,
        bool faltaConduccion = false)
    {
        lineas.Add(Texto(origen));
        lineas.Add(Texto(destino));
        lineas.Add(Texto(carga));
        lineas.Add(Numero(kilometros));
        lineas.Add(Numero(media));
        lineas.Add(Numero(maxima));
        lineas.Add(Numero(inicio.Ticks));
        lineas.Add(Numero(fin.Ticks));

        if (version >= 3)
        {
            lineas.Add(faltaVelocidad ? "1" : "0");
            lineas.Add(faltaConduccion ? "1" : "0");
        }

        return this;
    }

    /// <summary>Añade una línea tal cual (para fabricar archivos estropeados).</summary>
    public GeneradorDatAntiguo LineaLibre(string linea)
    {
        lineas.Add(linea);
        return this;
    }

    public override string ToString() => string.Join("\r\n", lineas) + "\r\n";

    public void GuardarEn(string ruta) =>
        File.WriteAllText(ruta, ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private static string Texto(string valor) => Convert.ToBase64String(Encoding.UTF8.GetBytes(valor));

    private static string Numero(long valor) => valor.ToString(CultureInfo.InvariantCulture);

    private static string Numero(double valor) => valor.ToString(CultureInfo.InvariantCulture);
}
