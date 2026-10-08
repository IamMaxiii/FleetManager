using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Idiomas;

namespace FleetManager.ViewModels;

public enum NivelAviso
{
    Informacion,
    Atencion,
    Urgente
}

public sealed record Aviso(NivelAviso Nivel, string Texto);

/// <summary>
/// Decide qué avisos mostrar a partir del estado del tacógrafo, ordenados del más
/// urgente al menos. No depende de WPF: se prueba con pruebas automáticas.
/// </summary>
public static class GeneradorAvisos
{
    /// <summary>Se avisa de la parada obligatoria cuando queda esto o menos.</summary>
    public static readonly TimeSpan AntelacionParada = TimeSpan.FromMinutes(15);

    /// <summary>Se avisa del límite para empezar el descanso diario con esta antelación.</summary>
    public static readonly TimeSpan AntelacionDescansoDiario = TimeSpan.FromHours(1);

    /// <summary>Se avisa del plazo del descanso semanal con esta antelación.</summary>
    public static readonly TimeSpan AntelacionDescansoSemanal = TimeSpan.FromHours(12);

    public static IReadOnlyList<Aviso> Calcular(EstadoTacografo estado, bool jornadaAbierta)
    {
        var avisos = new List<Aviso>();

        if (!jornadaAbierta || estado.Ahora == default)
        {
            return avisos;
        }

        bool descansando = estado.ActividadActual == Actividad.Descanso && estado.DescansoActual > TimeSpan.Zero;

        // Límites de conducción agotados.
        if (estado.ConduccionContinuaRestante == TimeSpan.Zero && !descansando)
        {
            avisos.Add(new(NivelAviso.Urgente, Textos.T("Aviso.PausaObligatoria")));
        }

        if (estado.ConduccionDiariaRestante == TimeSpan.Zero)
        {
            avisos.Add(new(NivelAviso.Urgente, Textos.T("Aviso.DiariaAgotada")));
        }

        if (estado.ConduccionSemanalRestante == TimeSpan.Zero || estado.ConduccionBisemanalRestante == TimeSpan.Zero)
        {
            avisos.Add(new(NivelAviso.Urgente, Textos.T("Aviso.SemanalAgotada")));
        }

        // Parada cercana.
        if (estado.SiguienteParada > TimeSpan.Zero && estado.SiguienteParada <= AntelacionParada && !descansando)
        {
            avisos.Add(new(NivelAviso.Atencion, Textos.T("Aviso.ParadaEn", (int)Math.Ceiling(estado.SiguienteParada.TotalMinutes))));
        }

        if (descansando)
        {
            // Durante el descanso: qué se ha cumplido ya.
            if (estado.DescansoDiarioRestante == TimeSpan.Zero)
            {
                avisos.Add(new(NivelAviso.Informacion, Textos.T("Aviso.DiarioCumplido")));
            }
            else if (estado.PausaRestante == TimeSpan.Zero)
            {
                avisos.Add(new(NivelAviso.Informacion, Textos.T("Aviso.PausaCumplida")));
            }
        }
        else
        {
            // Plazos de los descansos.
            if (estado.Ahora > estado.LimiteInicioDescansoDiario)
            {
                avisos.Add(new(NivelAviso.Urgente, Textos.T("Aviso.DiarioFueraPlazo")));
            }
            else if (estado.LimiteInicioDescansoDiario - estado.Ahora <= AntelacionDescansoDiario)
            {
                avisos.Add(new(NivelAviso.Atencion, Textos.T("Aviso.DiarioAntesDe", Formato.HoraJuego(estado.LimiteInicioDescansoDiario))));
            }

            if (!estado.DescansoSemanalEnCurso)
            {
                if (estado.Ahora > estado.PlazoDescansoSemanal)
                {
                    avisos.Add(new(NivelAviso.Urgente, Textos.T("Aviso.SemanalFueraPlazo")));
                }
                else if (estado.PlazoDescansoSemanal - estado.Ahora <= AntelacionDescansoSemanal)
                {
                    avisos.Add(new(NivelAviso.Atencion, Textos.T("Aviso.SemanalAntesDe", Formato.HoraJuego(estado.PlazoDescansoSemanal))));
                }
            }
        }

        return avisos.OrderByDescending(a => a.Nivel).ToList();
    }
}
