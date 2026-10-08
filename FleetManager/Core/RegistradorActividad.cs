using FleetManager.Models;

namespace FleetManager.Core;

/// <summary>
/// Qué ocurrió al apuntar una lectura en el registro.
/// </summary>
public enum ResultadoRegistro
{
    /// <summary>Lectura normal.</summary>
    Normal,

    /// <summary>El reloj del juego saltó hacia delante (por ejemplo, al dormir): el hueco se ha apuntado como descanso.</summary>
    SaltoAdelante,

    /// <summary>El reloj del juego retrocedió (por ejemplo, al cargar una partida): se ha borrado lo posterior.</summary>
    SaltoAtras
}

/// <summary>
/// Escribe el registro del tacógrafo a partir de lecturas "hora del juego + actividad".
///
/// - La actividad de una lectura vale desde esa hora hasta la siguiente lectura.
/// - Si el reloj salta hacia delante más de <see cref="ReglasUE.SaltoConsideradoDescanso"/>,
///   el hueco se apunta como descanso.
/// - Si el reloj retrocede, se borra del registro todo lo posterior a la nueva hora.
/// - Se descartan los periodos de más de <see cref="ReglasUE.AntiguedadMaximaRegistro"/>.
/// </summary>
public sealed class RegistradorActividad
{
    private readonly RegistroTacografo registro;

    public RegistradorActividad(RegistroTacografo registro)
    {
        this.registro = registro;
    }

    private List<PeriodoActividad> Periodos => registro.Periodos;

    public ResultadoRegistro Registrar(DateTime hora, Actividad actividad)
    {
        if (Periodos.Count == 0)
        {
            Anadir(actividad, hora);
            return ResultadoRegistro.Normal;
        }

        var resultado = ResultadoRegistro.Normal;
        DateTime ultimaHora = Periodos[^1].Fin;

        if (hora < ultimaHora)
        {
            BorrarDesde(hora);
            resultado = ResultadoRegistro.SaltoAtras;

            if (Periodos.Count == 0)
            {
                Anadir(actividad, hora);
                return resultado;
            }
        }
        else if (hora - ultimaHora > ReglasUE.SaltoConsideradoDescanso)
        {
            // El hueco es descanso; la actividad leída empieza ahora.
            Continuar(Actividad.Descanso, ultimaHora);
            Periodos[^1].Fin = hora;
            resultado = ResultadoRegistro.SaltoAdelante;
        }

        Continuar(actividad, hora);
        Purgar(hora);
        return resultado;
    }

    /// <summary>
    /// Cambia a <paramref name="actividad"/> todo lo registrado entre <paramref name="desde"/>
    /// y <paramref name="hasta"/> (la lectura actual). Se usa cuando una parada que se
    /// estaba contando como conducción resulta ser larga y pasa a otros trabajos.
    /// </summary>
    public void Reclasificar(DateTime desde, DateTime hasta, Actividad actividad)
    {
        if (Periodos.Count == 0 || hasta < desde)
        {
            Registrar(hasta, actividad);
            return;
        }

        BorrarDesde(desde);

        if (Periodos.Count == 0)
        {
            Anadir(actividad, desde);
        }
        else
        {
            Continuar(actividad, desde);
        }

        Periodos[^1].Fin = hasta;
        Purgar(hasta);
    }

    /// <summary>
    /// La actividad anterior dura hasta <paramref name="hora"/>; desde ahí sigue <paramref name="actividad"/>.
    /// </summary>
    private void Continuar(Actividad actividad, DateTime hora)
    {
        PeriodoActividad ultimo = Periodos[^1];
        ultimo.Fin = hora;

        if (ultimo.Actividad == actividad)
        {
            return;
        }

        if (ultimo.Duracion == TimeSpan.Zero)
        {
            // La actividad anterior no llegó a durar nada (dos cambios en el mismo minuto): se sustituye.
            Periodos.RemoveAt(Periodos.Count - 1);

            if (Periodos.Count > 0 && Periodos[^1].Actividad == actividad)
            {
                Periodos[^1].Fin = hora;
                return;
            }
        }

        Anadir(actividad, hora);
    }

    private void Anadir(Actividad actividad, DateTime hora) =>
        Periodos.Add(new PeriodoActividad { Actividad = actividad, Inicio = hora, Fin = hora });

    /// <summary>Borra todo lo registrado después de <paramref name="hora"/>.</summary>
    private void BorrarDesde(DateTime hora)
    {
        Periodos.RemoveAll(p => p.Inicio > hora);

        if (Periodos.Count > 0 && Periodos[^1].Fin > hora)
        {
            Periodos[^1].Fin = hora;
        }
    }

    private void Purgar(DateTime hora)
    {
        if (hora - DateTime.MinValue <= ReglasUE.AntiguedadMaximaRegistro)
        {
            return;
        }

        DateTime limite = hora - ReglasUE.AntiguedadMaximaRegistro;
        int antiguos = Periodos.FindIndex(p => p.Fin >= limite);

        if (antiguos > 0)
        {
            Periodos.RemoveRange(0, antiguos);
        }
    }
}
