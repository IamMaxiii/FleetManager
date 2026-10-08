using FleetManager.Models;
using FleetManager.Idiomas;

namespace FleetManager.Core;

/// <summary>
/// Calcula el estado del tacógrafo (contadores, plazos e infracciones) leyendo el
/// registro de actividades de principio a fin, según el Reglamento (CE) 561/2006.
///
/// Como todo sale del registro, los contadores nunca se "desincronizan": al abrir
/// la aplicación, tras un salto de reloj o tras editar el registro, basta con
/// volver a analizarlo.
///
/// Criterios:
/// - Solo el descanso cuenta como pausa o descanso; otros trabajos y disponibilidad no.
/// - Un descanso de 24 h o más es un descanso semanal (normal desde 45 h).
/// - Un descanso de 9 h o más es un descanso diario: normal si tiene 11 h y caben
///   dentro del plazo de 24 h, o si es la segunda parte (9 h) de uno dividido tras
///   otra de 3 h; si no, reducido.
/// - Un descanso que sigue en curso solo se da por bueno cuando ya cumple; mientras
///   pueda llegar a algo mejor (11 h, 45 h) no se cuenta como reducido.
/// - La ampliación a 10 h se gasta al pasar de 9 h y se cuenta en la semana en que ocurre.
/// </summary>
public static class AnalizadorTacografo
{
    public static EstadoTacografo Analizar(RegistroTacografo registro)
    {
        List<PeriodoActividad> periodos = registro.Periodos;

        if (periodos.Count == 0)
        {
            return EstadoInicial();
        }

        var analisis = new Analisis(periodos[0].Inicio);

        for (int i = 0; i < periodos.Count; i++)
        {
            PeriodoActividad periodo = periodos[i];

            if (periodo.Actividad == Actividad.Descanso)
            {
                analisis.Descanso(periodo.Inicio, periodo.Fin, terminado: i < periodos.Count - 1);
            }
            else
            {
                analisis.Trabajo(periodo);
            }
        }

        return analisis.Resultado(periodos[^1]);
    }

    private static EstadoTacografo EstadoInicial() => new()
    {
        ConduccionContinuaRestante = ReglasUE.ConduccionContinuaMaxima,
        PausaRestante = ReglasUE.PausaCompleta,
        LimiteConduccionDiaria = ReglasUE.ConduccionDiariaAmpliada,
        ConduccionDiariaRestante = ReglasUE.ConduccionDiariaAmpliada,
        AmpliacionesRestantes = ReglasUE.AmpliacionesPorSemana,
        DescansoDiarioRestante = ReglasUE.DescansoDiarioNormal,
        DescansosReducidosRestantes = ReglasUE.DescansosReducidosMaximos,
        ConduccionSemanalRestante = ReglasUE.ConduccionSemanalMaxima,
        ConduccionBisemanalRestante = ReglasUE.ConduccionBisemanalMaxima,
        SiguienteParada = ReglasUE.ConduccionContinuaMaxima
    };

    /// <summary>Recorrido del registro: guarda el estado intermedio mientras se lee.</summary>
    private sealed class Analisis
    {
        private readonly List<Infraccion> infracciones = [];
        private readonly Dictionary<DateTime, TimeSpan> conduccionPorSemana = [];
        private readonly Dictionary<DateTime, int> ampliacionesPorSemana = [];

        private DateTime inicioJornada;
        private DateTime finUltimoSemanal;
        private TimeSpan conduccionContinua;
        private TimeSpan conduccionDiaria;
        private bool primeraPartePausa;
        private bool primeraParteDividido;
        private bool ampliacionEnUso;
        private int reducidos;
        private bool haySemanalAnterior;
        private bool ultimoSemanalReducido;

        // Para no anotar la misma infracción varias veces seguidas.
        private bool marcadaContinua;
        private bool marcadaDiaria;
        private bool marcadoPlazoDiario;
        private bool marcadoPlazoSemanal;

        // Cómo estaba todo justo antes del descanso en curso (si el registro termina descansando).
        private bool primeraPartePausaAntesDelDescanso;
        private bool primeraParteDivididoAntesDelDescanso;

        public Analisis(DateTime inicioRegistro)
        {
            inicioJornada = inicioRegistro;
            finUltimoSemanal = inicioRegistro;
        }

        private DateTime PlazoDiario => inicioJornada + ReglasUE.PlazoDescansoDiario;

        /// <summary>Descanso mínimo que aún se puede hacer: 9 h si quedan reducidos o es la segunda parte de un dividido; si no, 11 h.</summary>
        private TimeSpan DescansoDiarioMinimo =>
            primeraParteDividido || reducidos < ReglasUE.DescansosReducidosMaximos
                ? ReglasUE.DescansoDiarioReducido
                : ReglasUE.DescansoDiarioNormal;

        private DateTime LimiteInicioDescansoDiario => PlazoDiario - DescansoDiarioMinimo;

        private DateTime PlazoSemanal => finUltimoSemanal + ReglasUE.PlazoDescansoSemanal;

        public void Trabajo(PeriodoActividad periodo)
        {
            if (!marcadoPlazoDiario && periodo.Fin > LimiteInicioDescansoDiario)
            {
                marcadoPlazoDiario = true;
                Anotar(
                    TipoInfraccion.DescansoDiarioFueraDePlazo,
                    LimiteInicioDescansoDiario,
                    Textos.T("Infraccion.DiarioFueraPlazo"));
            }

            if (!marcadoPlazoSemanal && periodo.Fin > PlazoSemanal)
            {
                marcadoPlazoSemanal = true;
                Anotar(
                    TipoInfraccion.DescansoSemanalFueraDePlazo,
                    PlazoSemanal,
                    Textos.T("Infraccion.SemanalFueraPlazo"));
            }

            if (periodo.Actividad == Actividad.Conduccion)
            {
                Conducir(periodo.Inicio, periodo.Fin);
            }
        }

        public void Descanso(DateTime inicio, DateTime fin, bool terminado)
        {
            TimeSpan duracion = fin - inicio;

            if (!terminado)
            {
                primeraPartePausaAntesDelDescanso = primeraPartePausa;
                primeraParteDivididoAntesDelDescanso = primeraParteDividido;
            }

            // Pausa: 45 min seguidos, o 15 min y después 30 min.
            if (duracion >= ReglasUE.PausaCompleta ||
                (primeraPartePausa && duracion >= ReglasUE.PausaSegundaParte))
            {
                ReiniciarConduccionContinua();
            }
            else if (duracion >= ReglasUE.PausaPrimeraParte)
            {
                primeraPartePausa = true;
            }

            // Descanso semanal.
            if (duracion >= ReglasUE.DescansoSemanalReducido)
            {
                if (!terminado && duracion < ReglasUE.DescansoSemanalNormal)
                {
                    // Aún puede llegar a 45 h: de momento cuenta como descanso diario cumplido.
                    NuevaJornada(fin);
                    return;
                }

                bool reducido = duracion < ReglasUE.DescansoSemanalNormal;

                if (reducido && haySemanalAnterior && ultimoSemanalReducido)
                {
                    Anotar(
                        TipoInfraccion.DescansosSemanalesReducidosSeguidos,
                        inicio,
                        Textos.T("Infraccion.SemanalesReducidos"));
                }

                haySemanalAnterior = true;
                ultimoSemanalReducido = reducido;
                finUltimoSemanal = fin;
                reducidos = 0;
                marcadoPlazoSemanal = false;
                NuevaJornada(fin);
                return;
            }

            // Descanso diario.
            if (duracion >= ReglasUE.DescansoDiarioReducido)
            {
                bool cabeNormalEnPlazo = inicio + ReglasUE.DescansoDiarioNormal <= PlazoDiario;
                bool normal = primeraParteDividido ||
                              (duracion >= ReglasUE.DescansoDiarioNormal && cabeNormalEnPlazo);

                if (!normal && !terminado && duracion < ReglasUE.DescansoDiarioNormal)
                {
                    // Aún puede llegar a 11 h: todavía no se cuenta como reducido.
                    return;
                }

                if (!normal)
                {
                    reducidos++;

                    if (reducidos > ReglasUE.DescansosReducidosMaximos)
                    {
                        Anotar(
                            TipoInfraccion.DemasiadosDescansosReducidos,
                            inicio,
                            Textos.T("Infraccion.DemasiadosReducidos"));
                    }
                }

                NuevaJornada(fin);
                return;
            }

            // Primera parte de un descanso diario dividido.
            if (duracion >= ReglasUE.DescansoDivididoPrimeraParte)
            {
                primeraParteDividido = true;
            }
        }

        public EstadoTacografo Resultado(PeriodoActividad ultimo)
        {
            DateTime ahora = ultimo.Fin;
            bool descansando = ultimo.Actividad == Actividad.Descanso;
            TimeSpan descansoActual = descansando ? ultimo.Duracion : TimeSpan.Zero;

            TimeSpan pausaRestante;

            if (descansando)
            {
                TimeSpan necesaria = primeraPartePausaAntesDelDescanso ? ReglasUE.PausaSegundaParte : ReglasUE.PausaCompleta;
                pausaRestante = Restante(necesaria, descansoActual);
            }
            else
            {
                pausaRestante = primeraPartePausa ? ReglasUE.PausaSegundaParte : ReglasUE.PausaCompleta;
            }

            bool divididoPendiente = descansando ? primeraParteDivididoAntesDelDescanso : primeraParteDividido;
            TimeSpan descansoNecesario = divididoPendiente ? ReglasUE.DescansoDivididoSegundaParte : ReglasUE.DescansoDiarioNormal;

            DateTime lunes = ReglasUE.InicioSemana(ahora);
            TimeSpan semanal = conduccionPorSemana.GetValueOrDefault(lunes);
            TimeSpan anterior = SemanaAnterior(lunes) is { } lunesAnterior
                ? conduccionPorSemana.GetValueOrDefault(lunesAnterior)
                : TimeSpan.Zero;

            int ampliacionesRestantes = Math.Max(0, ReglasUE.AmpliacionesPorSemana - ampliacionesPorSemana.GetValueOrDefault(lunes));
            TimeSpan limiteDiario = ampliacionEnUso || ampliacionesRestantes > 0
                ? ReglasUE.ConduccionDiariaAmpliada
                : ReglasUE.ConduccionDiariaMaxima;

            TimeSpan continuaRestante = Restante(ReglasUE.ConduccionContinuaMaxima, conduccionContinua);
            TimeSpan diariaRestante = Restante(limiteDiario, conduccionDiaria);
            TimeSpan semanalRestante = Restante(ReglasUE.ConduccionSemanalMaxima, semanal);
            TimeSpan bisemanalRestante = Restante(ReglasUE.ConduccionBisemanalMaxima, semanal + anterior);

            return new EstadoTacografo
            {
                Ahora = ahora,
                ActividadActual = ultimo.Actividad,
                TiempoActividadActual = ultimo.Duracion,

                ConduccionContinua = conduccionContinua,
                ConduccionContinuaRestante = continuaRestante,
                PrimeraPartePausaHecha = primeraPartePausa,
                PausaRestante = pausaRestante,

                ConduccionDiaria = conduccionDiaria,
                LimiteConduccionDiaria = limiteDiario,
                ConduccionDiariaRestante = diariaRestante,
                AmpliacionesRestantes = ampliacionesRestantes,
                InicioJornadaDiaria = inicioJornada,
                AmplitudJornada = ahora - inicioJornada,

                DescansoActual = descansoActual,
                DescansoDiarioRestante = Restante(descansoNecesario, descansoActual),
                PrimeraParteDescansoDivididoHecha = primeraParteDividido,
                DescansosReducidosRestantes = Math.Max(0, ReglasUE.DescansosReducidosMaximos - reducidos),
                PlazoDescansoDiario = PlazoDiario,
                LimiteInicioDescansoDiario = LimiteInicioDescansoDiario,

                ConduccionSemanal = semanal,
                ConduccionSemanalRestante = semanalRestante,
                ConduccionSemanaAnterior = anterior,
                ConduccionBisemanal = semanal + anterior,
                ConduccionBisemanalRestante = bisemanalRestante,

                PlazoDescansoSemanal = PlazoSemanal,
                DescansoSemanalEnCurso = descansoActual >= ReglasUE.DescansoSemanalReducido,

                SiguienteParada = Minimo(continuaRestante, diariaRestante, semanalRestante, bisemanalRestante),
                Infracciones = infracciones.OrderBy(i => i.Momento).ToList()
            };
        }

        /// <summary>Reparte la conducción por semanas (lunes 00:00) y la va sumando.</summary>
        private void Conducir(DateTime inicio, DateTime fin)
        {
            DateTime tramo = inicio;

            while (tramo < fin)
            {
                DateTime finSemana = ReglasUE.InicioSemana(tramo).AddDays(7);
                DateTime finTramo = fin < finSemana ? fin : finSemana;
                ConducirTramo(tramo, finTramo);
                tramo = finTramo;
            }
        }

        /// <summary>Suma un tramo de conducción que no cruza el cambio de semana.</summary>
        private void ConducirTramo(DateTime inicio, DateTime fin)
        {
            TimeSpan duracion = fin - inicio;

            // Conducción continua.
            if (!marcadaContinua &&
                Cruza(conduccionContinua, duracion, ReglasUE.ConduccionContinuaMaxima, out TimeSpan hastaContinua))
            {
                marcadaContinua = true;
                Anotar(
                    TipoInfraccion.ConduccionContinua,
                    inicio + hastaContinua,
                    Textos.T("Infraccion.Continua"));
            }

            conduccionContinua += duracion;

            // Conducción diaria: al pasar de 9 h se gasta una ampliación, si queda.
            if (!ampliacionEnUso && !marcadaDiaria &&
                Cruza(conduccionDiaria, duracion, ReglasUE.ConduccionDiariaMaxima, out TimeSpan hasta9))
            {
                DateTime momento = inicio + hasta9;
                DateTime semana = ReglasUE.InicioSemana(momento);
                int usadas = ampliacionesPorSemana.GetValueOrDefault(semana);

                if (usadas < ReglasUE.AmpliacionesPorSemana)
                {
                    ampliacionesPorSemana[semana] = usadas + 1;
                    ampliacionEnUso = true;
                }
                else
                {
                    marcadaDiaria = true;
                    Anotar(
                        TipoInfraccion.ConduccionDiaria,
                        momento,
                        Textos.T("Infraccion.Diaria9"));
                }
            }

            if (ampliacionEnUso && !marcadaDiaria &&
                Cruza(conduccionDiaria, duracion, ReglasUE.ConduccionDiariaAmpliada, out TimeSpan hasta10))
            {
                marcadaDiaria = true;
                Anotar(TipoInfraccion.ConduccionDiaria, inicio + hasta10, Textos.T("Infraccion.Diaria10"));
            }

            conduccionDiaria += duracion;

            // Conducción semanal y bisemanal.
            DateTime lunes = ReglasUE.InicioSemana(inicio);
            TimeSpan semanal = conduccionPorSemana.GetValueOrDefault(lunes);
            TimeSpan anterior = SemanaAnterior(lunes) is { } lunesAnterior
                ? conduccionPorSemana.GetValueOrDefault(lunesAnterior)
                : TimeSpan.Zero;

            if (Cruza(semanal, duracion, ReglasUE.ConduccionSemanalMaxima, out TimeSpan hastaSemanal))
            {
                Anotar(TipoInfraccion.ConduccionSemanal, inicio + hastaSemanal, Textos.T("Infraccion.Semanal"));
            }

            if (Cruza(semanal + anterior, duracion, ReglasUE.ConduccionBisemanalMaxima, out TimeSpan hastaBisemanal))
            {
                Anotar(TipoInfraccion.ConduccionBisemanal, inicio + hastaBisemanal, Textos.T("Infraccion.Bisemanal"));
            }

            conduccionPorSemana[lunes] = semanal + duracion;
        }

        private void NuevaJornada(DateTime inicio)
        {
            inicioJornada = inicio;
            conduccionDiaria = TimeSpan.Zero;
            ampliacionEnUso = false;
            primeraParteDividido = false;
            marcadaDiaria = false;
            marcadoPlazoDiario = false;
            ReiniciarConduccionContinua();
        }

        private void ReiniciarConduccionContinua()
        {
            conduccionContinua = TimeSpan.Zero;
            primeraPartePausa = false;
            marcadaContinua = false;
        }

        private void Anotar(TipoInfraccion tipo, DateTime momento, string descripcion) =>
            infracciones.Add(new Infraccion(tipo, momento, descripcion));
    }

    /// <summary>
    /// ¿Al sumar <paramref name="duracion"/> a <paramref name="antes"/> se pasa de <paramref name="limite"/>?
    /// Si es así, <paramref name="hastaElLimite"/> dice cuánto tiempo del tramo falta para pasarse.
    /// </summary>
    private static bool Cruza(TimeSpan antes, TimeSpan duracion, TimeSpan limite, out TimeSpan hastaElLimite)
    {
        hastaElLimite = limite - antes;
        return antes <= limite && antes + duracion > limite;
    }

    private static TimeSpan Restante(TimeSpan limite, TimeSpan usado) =>
        usado >= limite ? TimeSpan.Zero : limite - usado;

    private static TimeSpan Minimo(params TimeSpan[] valores) => valores.Min();

    /// <summary>Lunes de la semana anterior; vacío si no existe (la hora del juego empieza en el año 1).</summary>
    private static DateTime? SemanaAnterior(DateTime lunes) =>
        lunes.Ticks >= TimeSpan.TicksPerDay * 7 ? lunes.AddDays(-7) : null;
}
