using System.IO;
using System.Text;
using FleetManager.Models;

namespace FleetManager.Storage.Importacion;

/// <param name="Historial">Las jornadas importadas.</param>
/// <param name="Formato">Versión del formato antiguo encontrada (V1, V2 o V3).</param>
/// <param name="Avisos">Datos que hubo que completar o corregir al importar.</param>
public sealed record ResultadoImportacion(Historial Historial, string Formato, IReadOnlyList<string> Avisos);

/// <summary>
/// Importa los archivos de la versión antigua de FleetManager:
/// driving_sessions.dat (formatos V1, V2 y V3) y driver_profile.dat.
///
/// Solo lee: los archivos antiguos nunca se modifican. Si el archivo tiene un
/// error, no se importa nada (nada de importaciones a medias) y se lanza
/// <see cref="FormatoAntiguoException"/> indicando la línea.
/// </summary>
public static class ImportadorDatosAntiguos
{
    private const string CabeceraPerfil = "FLEETMANAGER_DRIVER_PROFILE_V1";

    public static ResultadoImportacion ImportarSesiones(string ruta)
    {
        using var archivo = new StreamReader(ruta, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lector = new LectorLineas(archivo);
        var avisos = new List<string>();

        int version = lector.Leer("la cabecera") switch
        {
            "FLEETMANAGER_SESSIONS_V1" => 1,
            "FLEETMANAGER_SESSIONS_V2" => 2,
            "FLEETMANAGER_SESSIONS_V3" => 3,
            _ => throw new FormatoAntiguoException(
                "no es un archivo de jornadas de FleetManager (cabecera desconocida).", lector.NumeroLinea)
        };

        int numeroJornadas = lector.LeerCantidad("el número de jornadas");
        var historial = new Historial();

        for (int i = 0; i < numeroJornadas; i++)
        {
            Jornada? jornada = LeerJornada(lector, version, avisos);

            if (jornada is not null)
            {
                historial.Jornadas.Add(jornada);
            }
        }

        DejarSoloUnaJornadaAbierta(historial, avisos);

        return new ResultadoImportacion(historial, $"V{version}", avisos);
    }

    public static PerfilConductor ImportarPerfil(string ruta)
    {
        using var archivo = new StreamReader(ruta, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lector = new LectorLineas(archivo);

        if (lector.Leer("la cabecera") != CabeceraPerfil)
        {
            throw new FormatoAntiguoException(
                "no es un archivo de perfil de FleetManager (cabecera desconocida).", lector.NumeroLinea);
        }

        return new PerfilConductor
        {
            Nombre = lector.LeerTexto("el nombre"),
            Apellidos = lector.LeerTexto("los apellidos"),
            FechaNacimiento = lector.LeerTexto("la fecha de nacimiento"),
            NumeroPermiso = lector.LeerTexto("el número de permiso"),
            NumeroAdr = lector.LeerTexto("el número ADR"),
            KilometrosTarjeta = lector.LeerDecimal("los kilómetros de la tarjeta")
        };
    }

    private static Jornada? LeerJornada(LectorLineas lector, int version, List<string> avisos)
    {
        string idTexto = lector.Leer("el identificador de la jornada");
        int numero = lector.LeerEntero("el número de jornada");
        DateTime? inicio = lector.LeerFecha("el inicio de la jornada");
        DateTime? fin = lector.LeerFecha("el fin de la jornada");
        bool abierta = lector.LeerSiNo("si la jornada está abierta");

        if (version >= 2)
        {
            SaltarDias(lector);
        }

        int numeroTrayectos = lector.LeerCantidad("el número de trayectos");
        var trayectos = new List<(Trayecto Trayecto, DateTime? Inicio, DateTime? Fin)>();

        for (int i = 0; i < numeroTrayectos; i++)
        {
            trayectos.Add(LeerTrayecto(lector, version));
        }

        // Completar fechas que faltaban en el archivo antiguo.
        inicio ??= trayectos.Select(t => t.Inicio).Where(f => f is not null).Min();

        if (inicio is null)
        {
            avisos.Add($"Jornada {numero}: no tenía fecha de inicio ni trayectos con fecha; no se ha importado.");
            return null;
        }

        var jornada = new Jornada
        {
            Id = Guid.TryParse(idTexto, out Guid id) ? id : Guid.NewGuid(),
            Numero = numero,
            Inicio = inicio.Value
        };

        foreach (var (trayecto, inicioTrayecto, finTrayecto) in trayectos)
        {
            trayecto.Inicio = inicioTrayecto ?? jornada.Inicio;
            trayecto.Fin = finTrayecto ?? trayecto.Inicio;
            jornada.Trayectos.Add(trayecto);
        }

        if (!abierta)
        {
            if (fin is null)
            {
                avisos.Add($"Jornada {numero}: estaba cerrada pero sin hora de fin; se ha usado la del último trayecto.");
            }

            jornada.Fin = fin ?? UltimaHoraConocida(jornada);
        }

        return jornada;
    }

    /// <summary>
    /// Los formatos V2 y V3 guardaban un resumen por día. Ahora los días se calculan
    /// a partir de los trayectos, así que se leen (para comprobar el formato) y se descartan.
    /// </summary>
    private static void SaltarDias(LectorLineas lector)
    {
        int numeroDias = lector.LeerCantidad("el número de días");

        for (int i = 0; i < numeroDias; i++)
        {
            lector.LeerFecha("la fecha del día");
            lector.LeerFecha("el inicio del día");
            lector.LeerFecha("el fin del día");
            lector.LeerDecimal("los kilómetros del día");
            lector.LeerCantidad("el número de trayectos del día");
        }
    }

    private static (Trayecto Trayecto, DateTime? Inicio, DateTime? Fin) LeerTrayecto(LectorLineas lector, int version)
    {
        var trayecto = new Trayecto
        {
            Origen = lector.LeerTexto("el origen del trayecto"),
            Destino = lector.LeerTexto("el destino del trayecto"),
            Carga = lector.LeerTexto("la carga del trayecto"),
            Kilometros = lector.LeerDecimal("los kilómetros del trayecto"),
            VelocidadMedia = lector.LeerDecimal("la velocidad media del trayecto"),
            VelocidadMaxima = lector.LeerDecimal("la velocidad máxima del trayecto")
        };

        DateTime? inicio = lector.LeerFecha("el inicio del trayecto");
        DateTime? fin = lector.LeerFecha("el fin del trayecto");

        if (version >= 3)
        {
            trayecto.FaltaVelocidad = lector.LeerSiNo("la falta de velocidad");
            trayecto.FaltaConduccion = lector.LeerSiNo("la falta de conducción");
        }

        return (trayecto, inicio, fin);
    }

    /// <summary>
    /// Solo puede haber una jornada abierta. Si el archivo antiguo tenía varias,
    /// se deja abierta la más reciente y se cierran las demás.
    /// </summary>
    private static void DejarSoloUnaJornadaAbierta(Historial historial, List<string> avisos)
    {
        var sobrantes = historial.Jornadas
            .Where(j => j.Abierta)
            .OrderBy(j => j.Inicio)
            .SkipLast(1)
            .ToList();

        foreach (Jornada jornada in sobrantes)
        {
            jornada.Fin = UltimaHoraConocida(jornada);
            avisos.Add($"Jornada {jornada.Numero}: estaba abierta junto con otra más reciente; se ha cerrado.");
        }
    }

    private static DateTime UltimaHoraConocida(Jornada jornada) =>
        jornada.Trayectos.Count == 0
            ? jornada.Inicio
            : jornada.Trayectos.Max(t => t.Fin);
}
