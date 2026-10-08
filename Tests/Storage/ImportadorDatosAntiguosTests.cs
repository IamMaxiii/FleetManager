using FleetManager.Models;
using FleetManager.Storage.Importacion;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Storage;

/// <summary>
/// Pruebas de la importación de los archivos .dat de la versión antigua.
/// Los archivos se fabrican con <see cref="GeneradorDatAntiguo"/>.
/// </summary>
public sealed class ImportadorDatosAntiguosTests : IDisposable
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 8, 0, 0);

    private readonly CarpetaTemporal carpeta = new();

    public void Dispose() => carpeta.Dispose();

    private string RutaDat => carpeta.Archivo("driving_sessions.dat");

    [Fact]
    public void Importa_el_formato_V1()
    {
        var id = Guid.NewGuid();

        GeneradorDatAntiguo.Sesiones(version: 1, numeroJornadas: 1)
            .Jornada(id, numero: 7, Lunes, Lunes.AddHours(9), abierta: false)
            .NumeroTrayectos(1)
            .Trayecto("Madrid", "Zaragoza", "Maquinaria", 312.4, 78.1, 89.6, Lunes.AddMinutes(30), Lunes.AddHours(4.5))
            .GuardarEn(RutaDat);

        var resultado = ImportadorDatosAntiguos.ImportarSesiones(RutaDat);

        Assert.Equal("V1", resultado.Formato);
        Assert.Empty(resultado.Avisos);

        Jornada jornada = Assert.Single(resultado.Historial.Jornadas);
        Assert.Equal(id, jornada.Id);
        Assert.Equal(7, jornada.Numero);
        Assert.Equal(Lunes, jornada.Inicio);
        Assert.Equal(Lunes.AddHours(9), jornada.Fin);

        Trayecto trayecto = Assert.Single(jornada.Trayectos);
        Assert.Equal("Madrid", trayecto.Origen);
        Assert.Equal("Zaragoza", trayecto.Destino);
        Assert.Equal("Maquinaria", trayecto.Carga);
        Assert.Equal(312.4, trayecto.Kilometros);
        Assert.Equal(78.1, trayecto.VelocidadMedia);
        Assert.Equal(89.6, trayecto.VelocidadMaxima);
        Assert.Equal(Lunes.AddMinutes(30), trayecto.Inicio);
        Assert.Equal(Lunes.AddHours(4.5), trayecto.Fin);
        Assert.False(trayecto.FaltaVelocidad);
        Assert.False(trayecto.FaltaConduccion);
    }

    [Fact]
    public void Importa_el_formato_V2_y_descarta_el_resumen_por_dias()
    {
        GeneradorDatAntiguo.Sesiones(version: 2, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, Lunes.AddDays(1), abierta: false)
            .Dias((Lunes, 300, 1), (Lunes.AddDays(1).Date, 120, 1))
            .NumeroTrayectos(2)
            .Trayecto("Lyon", "París", "Quesos", 300, 80, 90, Lunes.AddHours(1), Lunes.AddHours(5))
            .Trayecto("París", "Calais", "Quesos", 120, 82, 90, Lunes.AddHours(20), Lunes.AddHours(22))
            .GuardarEn(RutaDat);

        var resultado = ImportadorDatosAntiguos.ImportarSesiones(RutaDat);

        Assert.Equal("V2", resultado.Formato);
        Jornada jornada = Assert.Single(resultado.Historial.Jornadas);
        Assert.Equal(2, jornada.Trayectos.Count);
        Assert.Equal(420, jornada.Kilometros);
    }

    [Fact]
    public void Importa_el_formato_V3_con_las_faltas()
    {
        GeneradorDatAntiguo.Sesiones(version: 3, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, Lunes.AddHours(10), abierta: false)
            .Dias((Lunes, 250, 2))
            .NumeroTrayectos(2)
            .Trayecto("Berlín", "Hamburgo", "Coches", 150, 85, 97, Lunes.AddHours(1), Lunes.AddHours(3), faltaVelocidad: true)
            .Trayecto("Hamburgo", "Kiel", "Coches", 100, 80, 88, Lunes.AddHours(4), Lunes.AddHours(9.5), faltaConduccion: true)
            .GuardarEn(RutaDat);

        var resultado = ImportadorDatosAntiguos.ImportarSesiones(RutaDat);

        Assert.Equal("V3", resultado.Formato);
        var trayectos = Assert.Single(resultado.Historial.Jornadas).Trayectos;
        Assert.True(trayectos[0].FaltaVelocidad);
        Assert.False(trayectos[0].FaltaConduccion);
        Assert.False(trayectos[1].FaltaVelocidad);
        Assert.True(trayectos[1].FaltaConduccion);
    }

    [Fact]
    public void Conserva_acentos_y_caracteres_especiales()
    {
        GeneradorDatAntiguo.Sesiones(version: 3, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, Lunes.AddHours(9), abierta: false)
            .Dias()
            .NumeroTrayectos(1)
            .Trayecto("A Coruña", "Łódź", "Pimentón «de la Vera»; 20 t", 10, 50, 60, Lunes, Lunes.AddHours(1))
            .GuardarEn(RutaDat);

        Trayecto trayecto = Assert.Single(Assert.Single(ImportadorDatosAntiguos.ImportarSesiones(RutaDat).Historial.Jornadas).Trayectos);

        Assert.Equal("A Coruña", trayecto.Origen);
        Assert.Equal("Łódź", trayecto.Destino);
        Assert.Equal("Pimentón «de la Vera»; 20 t", trayecto.Carga);
    }

    [Fact]
    public void Una_jornada_abierta_sigue_abierta()
    {
        GeneradorDatAntiguo.Sesiones(version: 3, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, fin: null, abierta: true)
            .Dias((Lunes, 0, 0))
            .NumeroTrayectos(0)
            .GuardarEn(RutaDat);

        Jornada jornada = Assert.Single(ImportadorDatosAntiguos.ImportarSesiones(RutaDat).Historial.Jornadas);

        Assert.True(jornada.Abierta);
        Assert.Null(jornada.Fin);
    }

    [Fact]
    public void Si_habia_varias_jornadas_abiertas_solo_queda_abierta_la_mas_reciente()
    {
        GeneradorDatAntiguo.Sesiones(version: 1, numeroJornadas: 2)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, fin: null, abierta: true)
            .NumeroTrayectos(1)
            .Trayecto("Roma", "Nápoles", "Pasta", 230, 80, 90, Lunes.AddHours(1), Lunes.AddHours(4))
            .Jornada(Guid.NewGuid(), numero: 2, Lunes.AddDays(1), fin: null, abierta: true)
            .NumeroTrayectos(0)
            .GuardarEn(RutaDat);

        var resultado = ImportadorDatosAntiguos.ImportarSesiones(RutaDat);

        Assert.Equal(Lunes.AddHours(4), resultado.Historial.Jornadas[0].Fin);
        Assert.True(resultado.Historial.Jornadas[1].Abierta);
        Assert.Contains(resultado.Avisos, a => a.StartsWith("Jornada 1:"));
    }

    [Fact]
    public void Una_jornada_cerrada_sin_hora_de_fin_usa_la_del_ultimo_trayecto()
    {
        GeneradorDatAntiguo.Sesiones(version: 1, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 4, Lunes, fin: null, abierta: false)
            .NumeroTrayectos(1)
            .Trayecto("Oslo", "Bergen", "Pescado", 460, 70, 80, Lunes.AddHours(1), Lunes.AddHours(8))
            .GuardarEn(RutaDat);

        var resultado = ImportadorDatosAntiguos.ImportarSesiones(RutaDat);

        Assert.Equal(Lunes.AddHours(8), Assert.Single(resultado.Historial.Jornadas).Fin);
        Assert.Single(resultado.Avisos);
    }

    [Fact]
    public void Importa_todas_las_jornadas_aunque_sean_mas_de_100()
    {
        var generador = GeneradorDatAntiguo.Sesiones(version: 1, numeroJornadas: 150);

        for (int i = 1; i <= 150; i++)
        {
            DateTime inicio = Lunes.AddDays(i);
            generador.Jornada(Guid.NewGuid(), i, inicio, inicio.AddHours(9), abierta: false).NumeroTrayectos(0);
        }

        generador.GuardarEn(RutaDat);

        var resultado = ImportadorDatosAntiguos.ImportarSesiones(RutaDat);

        Assert.Equal(150, resultado.Historial.Jornadas.Count);
        Assert.Equal(1, resultado.Historial.Jornadas[0].Numero);
        Assert.Equal(150, resultado.Historial.Jornadas[^1].Numero);
    }

    [Fact]
    public void Un_archivo_cortado_no_importa_nada_y_dice_la_linea()
    {
        string completo = GeneradorDatAntiguo.Sesiones(version: 1, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, Lunes.AddHours(9), abierta: false)
            .NumeroTrayectos(1)
            .Trayecto("Madrid", "Valencia", "Naranjas", 350, 80, 90, Lunes.AddHours(1), Lunes.AddHours(5))
            .ToString();

        // Se corta el archivo a mitad del trayecto (como si se hubiera guardado a medias).
        string[] lineas = completo.Split("\r\n");
        File.WriteAllLines(RutaDat, lineas.Take(10));

        var error = Assert.Throws<FormatoAntiguoException>(() => ImportadorDatosAntiguos.ImportarSesiones(RutaDat));

        Assert.Equal(11, error.Linea);
        Assert.Contains("se acaba antes de tiempo", error.Message);
    }

    [Fact]
    public void Un_valor_incorrecto_no_importa_nada_y_dice_la_linea()
    {
        GeneradorDatAntiguo.Sesiones(version: 1, numeroJornadas: 1)
            .Jornada(Guid.NewGuid(), numero: 1, Lunes, Lunes.AddHours(9), abierta: false)
            .LineaLibre("dos") // número de trayectos que no es un número
            .GuardarEn(RutaDat);

        var error = Assert.Throws<FormatoAntiguoException>(() => ImportadorDatosAntiguos.ImportarSesiones(RutaDat));

        Assert.Equal(8, error.Linea);
        Assert.Contains("\"dos\"", error.Message);
    }

    [Fact]
    public void Una_cabecera_desconocida_se_rechaza()
    {
        File.WriteAllText(RutaDat, "OTRO_PROGRAMA_V1\r\n0\r\n");

        var error = Assert.Throws<FormatoAntiguoException>(() => ImportadorDatosAntiguos.ImportarSesiones(RutaDat));

        Assert.Equal(1, error.Linea);
    }

    [Fact]
    public void Importa_el_perfil_del_conductor()
    {
        string ruta = carpeta.Archivo("driver_profile.dat");
        File.WriteAllText(ruta, GeneradorDatAntiguo.Perfil("Ana", "García Pérez", "05/04/1990", "ES-123456", "ADR-987654", 12345.6));

        PerfilConductor perfil = ImportadorDatosAntiguos.ImportarPerfil(ruta);

        Assert.Equal("Ana", perfil.Nombre);
        Assert.Equal("García Pérez", perfil.Apellidos);
        Assert.Equal("05/04/1990", perfil.FechaNacimiento);
        Assert.Equal("ES-123456", perfil.NumeroPermiso);
        Assert.Equal("ADR-987654", perfil.NumeroAdr);
        Assert.Equal(12345.6, perfil.KilometrosTarjeta);
    }
}
