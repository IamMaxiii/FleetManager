using FleetManager.Core;
using FleetManager.Tests.Utilidades;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.Core;

/// <summary>
/// Conducción diaria (9 h, ampliable a 10 h dos veces por semana) y amplitud de jornada.
/// </summary>
public class ConduccionDiariaTests
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 6, 0, 0);

    [Fact]
    public void La_conduccion_diaria_no_se_pone_a_cero_a_medianoche()
    {
        EstadoTacografo estado = new ConstructorRegistro(new DateTime(2026, 3, 2, 20, 0, 0))
            .Conducir(H(3)).Descansar(M(45)).Conducir(H(3)) // termina el martes a las 02:45
            .Analizar();

        Assert.Equal(H(6), estado.ConduccionDiaria);
        Assert.Equal(new DateTime(2026, 3, 3, 2, 45, 0), estado.Ahora);
    }

    [Fact]
    public void Un_descanso_diario_de_11h_empieza_una_nueva_jornada()
    {
        var registro = new ConstructorRegistro(Lunes).ConducirConPausas(H(8)).Descansar(H(11));
        DateTime finDescanso = registro.Hora;

        EstadoTacografo estado = registro.Conducir(H(1)).Analizar();

        Assert.Equal(H(1), estado.ConduccionDiaria);
        Assert.Equal(H(1), estado.ConduccionContinua);
        Assert.Equal(finDescanso, estado.InicioJornadaDiaria);
        Assert.Equal(H(1), estado.AmplitudJornada);
    }

    [Fact]
    public void Pasar_de_9h_gasta_una_ampliacion_y_no_es_infraccion()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes).ConducirConPausas(H(9.5)).Analizar();

        Assert.Equal(H(9.5), estado.ConduccionDiaria);
        Assert.Equal(1, estado.AmpliacionesRestantes);
        Assert.Equal(H(10), estado.LimiteConduccionDiaria);
        Assert.Equal(M(30), estado.ConduccionDiariaRestante);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Justo_9h_no_gasta_ampliacion()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes).ConducirConPausas(H(9)).Analizar();

        Assert.Equal(2, estado.AmpliacionesRestantes);
    }

    [Fact]
    public void Pasar_de_10h_es_infraccion()
    {
        var registro = new ConstructorRegistro(Lunes).ConducirConPausas(H(10.5));
        DateTime diezHoras = Lunes + H(10) + M(45) + M(45); // 10 h de conducción + 2 pausas

        Infraccion infraccion = Assert.Single(registro.Analizar().Infracciones);

        Assert.Equal(TipoInfraccion.ConduccionDiaria, infraccion.Tipo);
        Assert.Equal(diezHoras, infraccion.Momento);
    }

    [Fact]
    public void La_tercera_ampliacion_de_la_semana_es_infraccion()
    {
        var registro = new ConstructorRegistro(Lunes);

        for (int dia = 0; dia < 3; dia++)
        {
            registro.ConducirConPausas(H(9.5)).DescansarHasta(Lunes.AddDays(dia + 1));
        }

        EstadoTacografo estado = registro.Analizar();

        Infraccion infraccion = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.ConduccionDiaria, infraccion.Tipo);
        Assert.Equal(Lunes.AddDays(2) + H(9) + M(45) + M(45), infraccion.Momento);
        Assert.Equal(0, estado.AmpliacionesRestantes);
    }

    [Fact]
    public void Las_ampliaciones_se_renuevan_el_lunes()
    {
        var sabado = new DateTime(2026, 3, 7, 6, 0, 0);
        var registro = new ConstructorRegistro(sabado);

        // Sábado, domingo y lunes con 9 h 30 de conducción.
        for (int dia = 0; dia < 3; dia++)
        {
            registro.ConducirConPausas(H(9.5)).DescansarHasta(sabado.AddDays(dia + 1));
        }

        EstadoTacografo estado = registro.Analizar();

        Assert.DoesNotContain(estado.Infracciones, i => i.Tipo == TipoInfraccion.ConduccionDiaria);
        Assert.Equal(1, estado.AmpliacionesRestantes); // la del lunes es de la semana nueva
    }

    [Fact]
    public void Sin_ampliaciones_el_limite_es_9h()
    {
        var registro = new ConstructorRegistro(Lunes);

        for (int dia = 0; dia < 2; dia++)
        {
            registro.ConducirConPausas(H(9.5)).DescansarHasta(Lunes.AddDays(dia + 1));
        }

        EstadoTacografo estado = registro.ConducirConPausas(H(8)).Analizar();

        Assert.Equal(H(9), estado.LimiteConduccionDiaria);
        Assert.Equal(H(1), estado.ConduccionDiariaRestante);
    }

    [Fact]
    public void Otros_trabajos_no_suman_conduccion_pero_si_amplitud()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes).Conducir(H(2)).OtrosTrabajos(H(3)).Analizar();

        Assert.Equal(H(2), estado.ConduccionDiaria);
        Assert.Equal(H(5), estado.AmplitudJornada);
    }
}
