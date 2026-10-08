using FleetManager.Idiomas;

namespace FleetManager.ViewModels;

/// <summary>
/// Color de una barra: normal, atención (queda poco), agotado, u objetivo (una
/// barra que se quiere llenar, como el descanso).
/// </summary>
public enum NivelIndicador
{
    Normal,
    Atencion,
    Agotado,
    Objetivo
}

/// <summary>
/// Una barra de tiempo del tacógrafo (conducción continua, diaria, descanso...).
/// Se actualiza en su sitio, sin crear objetos nuevos, para que la interfaz solo
/// redibuje lo que cambia.
/// </summary>
public sealed class IndicadorTiempo : ObjetoObservable
{
    /// <summary>A partir de esta parte del límite gastada, la barra pasa a "atención".</summary>
    public const double UmbralAtencion = 0.85;

    private string usado = "";
    private string limite = "";
    private string restante = "";
    private double fraccion;
    private NivelIndicador nivel;

    public IndicadorTiempo(string titulo)
    {
        Titulo = titulo;
    }

    public string Titulo { get; }

    public string Usado { get => usado; private set => Asignar(ref usado, value); }

    public string Limite { get => limite; private set => Asignar(ref limite, value); }

    public string Restante { get => restante; private set => Asignar(ref restante, value); }

    /// <summary>Parte llena de la barra, de 0 a 1.</summary>
    public double Fraccion { get => fraccion; private set => Asignar(ref fraccion, value); }

    public NivelIndicador Nivel { get => nivel; private set => Asignar(ref nivel, value); }

    /// <summary>Barra de un límite que no se debe superar (conducción).</summary>
    public void ActualizarLimite(TimeSpan usadoAhora, TimeSpan limiteAhora, TimeSpan restanteAhora)
    {
        double parte = limiteAhora > TimeSpan.Zero ? usadoAhora / limiteAhora : 0;

        Usado = Formato.Duracion(usadoAhora);
        Limite = Formato.Duracion(limiteAhora);
        Restante = Textos.T("Indicador.Quedan", Formato.Duracion(restanteAhora));
        Fraccion = Math.Clamp(parte, 0, 1);
        Nivel = restanteAhora <= TimeSpan.Zero ? NivelIndicador.Agotado
            : parte >= UmbralAtencion ? NivelIndicador.Atencion
            : NivelIndicador.Normal;
    }

    /// <summary>Barra de algo que hay que completar (descanso).</summary>
    public void ActualizarObjetivo(TimeSpan hecho, TimeSpan objetivo, TimeSpan falta)
    {
        Usado = Formato.Duracion(hecho);
        Limite = Formato.Duracion(objetivo);
        Restante = falta > TimeSpan.Zero ? Textos.T("Indicador.Faltan", Formato.Duracion(falta)) : Textos.T("Indicador.Cumplido");
        Fraccion = objetivo > TimeSpan.Zero ? Math.Clamp(hecho / objetivo, 0, 1) : 0;
        Nivel = NivelIndicador.Objetivo;
    }
}

/// <summary>
/// Una barra en porcentaje (combustible, AdBlue, desgastes).
/// </summary>
public sealed class IndicadorPorcentaje : ObjetoObservable
{
    private string texto = "";
    private double fraccion;
    private NivelIndicador nivel;

    public IndicadorPorcentaje(string titulo)
    {
        Titulo = titulo;
    }

    public string Titulo { get; }

    public string Texto { get => texto; private set => Asignar(ref texto, value); }

    public double Fraccion { get => fraccion; private set => Asignar(ref fraccion, value); }

    public NivelIndicador Nivel { get => nivel; private set => Asignar(ref nivel, value); }

    /// <summary>Depósitos: lleno es bueno. Atención por debajo del 25 %, agotado por debajo del 10 %.</summary>
    public void ActualizarDeposito(double cantidad, double capacidad, string unidad)
    {
        double parte = capacidad > 0 ? Math.Clamp(cantidad / capacidad, 0, 1) : 0;

        Fraccion = parte;
        Texto = $"{Formato.Numero(cantidad)} / {Formato.Numero(capacidad)} {unidad} ({Formato.Porcentaje(parte)})";
        Nivel = parte < 0.10 ? NivelIndicador.Agotado
            : parte < 0.25 ? NivelIndicador.Atencion
            : NivelIndicador.Normal;
    }

    /// <summary>Desgastes (0 a 1): poco es bueno. Atención desde el 10 %, agotado desde el 25 %.</summary>
    public void ActualizarDesgaste(double desgaste)
    {
        double parte = Math.Clamp(desgaste, 0, 1);

        Fraccion = parte;
        Texto = Formato.Porcentaje(parte);
        Nivel = parte >= 0.25 ? NivelIndicador.Agotado
            : parte >= 0.10 ? NivelIndicador.Atencion
            : NivelIndicador.Normal;
    }
}
