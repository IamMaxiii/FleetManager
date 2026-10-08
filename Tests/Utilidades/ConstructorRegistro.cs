using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Monta registros del tacógrafo para las pruebas, uno detrás de otro:
/// <c>new ConstructorRegistro(lunes).Conducir(H(4.5)).Descansar(M(45))</c>.
/// </summary>
public sealed class ConstructorRegistro
{
    private readonly RegistroTacografo registro = new();

    public ConstructorRegistro(DateTime inicio)
    {
        Hora = inicio;
    }

    /// <summary>Hora a la que termina lo montado hasta ahora.</summary>
    public DateTime Hora { get; private set; }

    public RegistroTacografo Registro => registro;

    public static TimeSpan H(double horas) => TimeSpan.FromHours(horas);

    public static TimeSpan M(double minutos) => TimeSpan.FromMinutes(minutos);

    public ConstructorRegistro Conducir(TimeSpan duracion) => Anadir(Actividad.Conduccion, duracion);

    public ConstructorRegistro Descansar(TimeSpan duracion) => Anadir(Actividad.Descanso, duracion);

    public ConstructorRegistro OtrosTrabajos(TimeSpan duracion) => Anadir(Actividad.OtrosTrabajos, duracion);

    public ConstructorRegistro Disponibilidad(TimeSpan duracion) => Anadir(Actividad.Disponibilidad, duracion);

    public ConstructorRegistro DescansarHasta(DateTime hora) => Descansar(hora - Hora);

    /// <summary>
    /// Conduce <paramref name="total"/> en tramos de 4 h 30 como mucho, con una pausa
    /// de 45 min entre tramos (como haría un conductor que cumple las pausas).
    /// </summary>
    public ConstructorRegistro ConducirConPausas(TimeSpan total)
    {
        TimeSpan pendiente = total;

        while (pendiente > TimeSpan.Zero)
        {
            TimeSpan tramo = pendiente < ReglasUE.ConduccionContinuaMaxima ? pendiente : ReglasUE.ConduccionContinuaMaxima;
            Conducir(tramo);
            pendiente -= tramo;

            if (pendiente > TimeSpan.Zero)
            {
                Descansar(ReglasUE.PausaCompleta);
            }
        }

        return this;
    }

    public EstadoTacografo Analizar() => AnalizadorTacografo.Analizar(registro);

    private ConstructorRegistro Anadir(Actividad actividad, TimeSpan duracion)
    {
        if (registro.Periodos.Count > 0 && registro.Periodos[^1].Actividad == actividad)
        {
            registro.Periodos[^1].Fin += duracion;
        }
        else
        {
            registro.Periodos.Add(new PeriodoActividad { Actividad = actividad, Inicio = Hora, Fin = Hora + duracion });
        }

        Hora += duracion;
        return this;
    }
}
