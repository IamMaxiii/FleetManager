using FleetManager.Core;
using FleetManager.Tests.Utilidades;
using FleetManager.ViewModels;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Qué avisos se muestran según el estado del tacógrafo.
/// </summary>
public class GeneradorAvisosTests
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 6, 0, 0);

    private static IReadOnlyList<Aviso> Avisos(ConstructorRegistro registro, bool jornadaAbierta = true) =>
        GeneradorAvisos.Calcular(registro.Analizar(), jornadaAbierta);

    [Fact]
    public void Sin_jornada_no_hay_avisos()
    {
        Assert.Empty(Avisos(new ConstructorRegistro(Lunes).Conducir(H(4.5)), jornadaAbierta: false));
    }

    [Fact]
    public void Al_principio_de_la_jornada_no_hay_avisos()
    {
        Assert.Empty(Avisos(new ConstructorRegistro(Lunes).Conducir(H(1))));
    }

    [Fact]
    public void Se_avisa_de_la_parada_15_min_antes()
    {
        Aviso aviso = Assert.Single(Avisos(new ConstructorRegistro(Lunes).Conducir(H(4) + M(20))));

        Assert.Equal(NivelAviso.Atencion, aviso.Nivel);
        Assert.Equal("Parada obligatoria en 10 min.", aviso.Texto);
    }

    [Fact]
    public void Al_llegar_a_4h30_la_pausa_es_urgente_y_va_primero()
    {
        IReadOnlyList<Aviso> avisos = Avisos(new ConstructorRegistro(Lunes).Conducir(H(4.5)));

        Assert.Equal(NivelAviso.Urgente, avisos[0].Nivel);
        Assert.StartsWith("Pausa obligatoria", avisos[0].Texto);
    }

    [Fact]
    public void Durante_la_pausa_se_avisa_cuando_esta_cumplida()
    {
        Aviso aviso = Assert.Single(Avisos(new ConstructorRegistro(Lunes).Conducir(H(4)).Descansar(M(46))));

        Assert.Equal(NivelAviso.Informacion, aviso.Nivel);
        Assert.StartsWith("Pausa cumplida", aviso.Texto);
    }

    [Fact]
    public void Durante_el_descanso_diario_se_avisa_cuando_esta_cumplido()
    {
        Aviso aviso = Assert.Single(Avisos(new ConstructorRegistro(Lunes).ConducirConPausas(H(8)).Descansar(H(11))));

        Assert.Equal("Descanso diario cumplido.", aviso.Texto);
    }

    [Fact]
    public void Se_avisa_una_hora_antes_del_limite_para_empezar_el_descanso_diario()
    {
        // 14 h 30 de jornada: el descanso de 9 h tiene que empezar como tarde a las 15 h.
        var registro = new ConstructorRegistro(Lunes).ConducirConPausas(H(8.5)).OtrosTrabajos(H(5.25));

        Aviso aviso = Assert.Single(Avisos(registro), a => a.Texto.StartsWith("Empieza el descanso diario"));

        Assert.Equal(NivelAviso.Atencion, aviso.Nivel);
    }

    [Fact]
    public void Pasado_el_limite_el_descanso_diario_es_urgente()
    {
        var registro = new ConstructorRegistro(Lunes).ConducirConPausas(H(8.5)).OtrosTrabajos(H(7));

        Assert.Contains(Avisos(registro), a => a is { Nivel: NivelAviso.Urgente, Texto: "Descanso diario fuera de plazo." });
    }
}
