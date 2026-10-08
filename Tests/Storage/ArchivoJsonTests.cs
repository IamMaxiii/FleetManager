using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Storage;

/// <summary>
/// Pruebas de la lectura y escritura segura de un archivo JSON.
/// </summary>
public sealed class ArchivoJsonTests : IDisposable
{
    private readonly CarpetaTemporal carpeta = new();
    private readonly RelojFalso reloj = new();
    private readonly ArchivoJson<Historial> archivo;

    public ArchivoJsonTests()
    {
        archivo = CrearArchivo();
    }

    private string Ruta => carpeta.Archivo("historial.json");

    private string RutaBak => Ruta + ".bak";

    private string CarpetaCopias => carpeta.Archivo("copias");

    public void Dispose() => carpeta.Dispose();

    [Fact]
    public void Guardar_y_leer_devuelve_los_mismos_datos()
    {
        Historial original = HistorialDePrueba(numeroJornadas: 1);

        archivo.Guardar(original);
        var resultado = CrearArchivo().Leer();

        Assert.Equal(OrigenDatos.ArchivoPrincipal, resultado.Origen);
        Assert.Null(resultado.Aviso);

        Jornada esperada = original.Jornadas[0];
        Jornada leida = Assert.Single(resultado.Datos.Jornadas);
        Assert.Equal(esperada.Id, leida.Id);
        Assert.Equal(esperada.Numero, leida.Numero);
        Assert.Equal(esperada.Inicio, leida.Inicio);
        Assert.Equal(esperada.Fin, leida.Fin);

        Trayecto trayectoEsperado = esperada.Trayectos[0];
        Trayecto trayectoLeido = Assert.Single(leida.Trayectos);
        Assert.Equal(trayectoEsperado.Origen, trayectoLeido.Origen);
        Assert.Equal(trayectoEsperado.Destino, trayectoLeido.Destino);
        Assert.Equal(trayectoEsperado.Carga, trayectoLeido.Carga);
        Assert.Equal(trayectoEsperado.Kilometros, trayectoLeido.Kilometros);
        Assert.Equal(trayectoEsperado.VelocidadMedia, trayectoLeido.VelocidadMedia);
        Assert.Equal(trayectoEsperado.VelocidadMaxima, trayectoLeido.VelocidadMaxima);
        Assert.Equal(trayectoEsperado.Inicio, trayectoLeido.Inicio);
        Assert.Equal(trayectoEsperado.Fin, trayectoLeido.Fin);
        Assert.True(trayectoLeido.FaltaVelocidad);
        Assert.False(trayectoLeido.FaltaConduccion);
    }

    [Fact]
    public void El_archivo_se_puede_leer_con_el_bloc_de_notas_y_conserva_los_acentos()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));

        string texto = File.ReadAllText(Ruta);

        Assert.Contains("\"Version\": 1", texto);
        Assert.Contains("Córdoba", texto);
        Assert.Contains("Málaga", texto);
    }

    [Fact]
    public void Sin_archivos_empieza_de_cero_sin_avisos()
    {
        var resultado = archivo.Leer();

        Assert.Equal(OrigenDatos.Nuevo, resultado.Origen);
        Assert.Null(resultado.Aviso);
        Assert.Empty(resultado.Datos.Jornadas);
        Assert.False(archivo.GuardadoBloqueado);
    }

    [Fact]
    public void El_primer_guardado_no_crea_copia_anterior()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));

        Assert.True(File.Exists(Ruta));
        Assert.False(File.Exists(RutaBak));
        Assert.False(File.Exists(Ruta + ".tmp"));
    }

    [Fact]
    public void El_segundo_guardado_deja_la_version_anterior_en_bak()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 2));

        Assert.Equal(2, LeerDirectamente(Ruta).Jornadas.Count);
        Assert.Single(LeerDirectamente(RutaBak).Jornadas);
    }

    [Fact]
    public void Un_temporal_sobrante_de_un_cierre_brusco_no_afecta()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));
        File.WriteAllText(Ruta + ".tmp", "{ esto es un guardado a medias");

        var resultado = CrearArchivo().Leer();

        Assert.Equal(OrigenDatos.ArchivoPrincipal, resultado.Origen);
        Assert.Single(resultado.Datos.Jornadas);

        // El siguiente guardado sobrescribe el temporal sin problemas.
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 3));
        Assert.Equal(3, LeerDirectamente(Ruta).Jornadas.Count);
        Assert.False(File.Exists(Ruta + ".tmp"));
    }

    [Fact]
    public void Un_archivo_danado_se_recupera_del_bak_y_se_conserva_aparte()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 2));
        File.WriteAllText(Ruta, "{ \"Version\": 1, \"Datos\": { \"Jornadas\": [ {");

        var lector = CrearArchivo();
        var resultado = lector.Leer();

        Assert.Equal(OrigenDatos.CopiaAnterior, resultado.Origen);
        Assert.Single(resultado.Datos.Jornadas);
        Assert.NotNull(resultado.Aviso);
        Assert.Contains("danado", resultado.Aviso);
        Assert.False(lector.GuardadoBloqueado);

        // El archivo dañado no se ha borrado: está apartado con otro nombre y su contenido intacto.
        string danado = Assert.Single(Directory.GetFiles(carpeta.Ruta, "historial-danado-*-principal.json"));
        Assert.StartsWith("{ \"Version\": 1", File.ReadAllText(danado));
        Assert.False(File.Exists(Ruta));
    }

    [Fact]
    public void Si_falta_el_principal_se_usa_el_bak_y_se_avisa()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 2));
        File.Delete(Ruta);

        var resultado = CrearArchivo().Leer();

        Assert.Equal(OrigenDatos.CopiaAnterior, resultado.Origen);
        Assert.Single(resultado.Datos.Jornadas);
        Assert.Contains("No se encontró historial.json", resultado.Aviso);
    }

    [Fact]
    public void Si_fallan_el_principal_y_el_bak_se_usa_la_copia_diaria_mas_reciente()
    {
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 1)); // copia del día 7
        reloj.Avanzar(TimeSpan.FromDays(1));
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 2)); // copia del día 8
        archivo.Guardar(HistorialDePrueba(numeroJornadas: 3));
        File.WriteAllText(Ruta, "dañado");
        File.WriteAllText(RutaBak, "");

        var resultado = CrearArchivo().Leer();

        Assert.Equal(OrigenDatos.CopiaDiaria, resultado.Origen);
        Assert.Equal(2, resultado.Datos.Jornadas.Count);
        Assert.Contains("historial-2026-10-08.json", resultado.Aviso);
        Assert.Single(Directory.GetFiles(carpeta.Ruta, "historial-danado-*-principal.json"));
        Assert.Single(Directory.GetFiles(carpeta.Ruta, "historial-danado-*-anterior.json"));
    }

    [Fact]
    public void Si_no_hay_nada_recuperable_empieza_vacio_con_aviso_y_conserva_los_danados()
    {
        File.WriteAllText(Ruta, "no es JSON");

        var lector = CrearArchivo();
        var resultado = lector.Leer();

        Assert.Equal(OrigenDatos.Nuevo, resultado.Origen);
        Assert.Empty(resultado.Datos.Jornadas);
        Assert.NotNull(resultado.Aviso);
        Assert.Single(Directory.GetFiles(carpeta.Ruta, "historial-danado-*.json"));

        // Ya no queda nada que se pueda pisar: se puede volver a guardar.
        Assert.False(lector.GuardadoBloqueado);
        lector.Guardar(HistorialDePrueba(numeroJornadas: 1));
    }

    [Fact]
    public void Un_archivo_de_una_version_mas_nueva_bloquea_el_guardado_y_no_se_toca()
    {
        const string contenido = "{ \"Version\": 99, \"Datos\": { \"Jornadas\": [] } }";
        File.WriteAllText(Ruta, contenido);

        var lector = CrearArchivo();
        var resultado = lector.Leer();

        Assert.True(lector.GuardadoBloqueado);
        Assert.Contains("versión más nueva", resultado.Aviso);
        Assert.Throws<InvalidOperationException>(() => lector.Guardar(HistorialDePrueba(numeroJornadas: 1)));
        Assert.Equal(contenido, File.ReadAllText(Ruta));
    }

    [Fact]
    public void Hace_una_copia_diaria_por_dia_y_conserva_las_10_ultimas()
    {
        for (int dia = 0; dia < 12; dia++)
        {
            archivo.Guardar(HistorialDePrueba(numeroJornadas: 1));
            archivo.Guardar(HistorialDePrueba(numeroJornadas: 2)); // segundo guardado del mismo día: no hace otra copia
            reloj.Avanzar(TimeSpan.FromDays(1));
        }

        string[] copias = Directory.GetFiles(CarpetaCopias).Select(r => Path.GetFileName(r)!).Order().ToArray();

        Assert.Equal(ArchivoJson<Historial>.CopiasDiariasAConservar, copias.Length);
        Assert.Equal("historial-2026-10-09.json", copias[0]);
        Assert.Equal("historial-2026-10-18.json", copias[^1]);
    }

    private ArchivoJson<Historial> CrearArchivo() =>
        new(Ruta, CarpetaCopias, new Registro(carpeta.Archivo("registro.log")), reloj);

    private Historial LeerDirectamente(string ruta)
    {
        string temporal = carpeta.Archivo($"lectura-{Guid.NewGuid():N}.json");
        File.Copy(ruta, temporal);
        var lector = new ArchivoJson<Historial>(temporal, carpeta.Archivo("sin-copias"), new Registro(carpeta.Archivo("registro.log")), reloj);
        var resultado = lector.Leer();
        Assert.Equal(OrigenDatos.ArchivoPrincipal, resultado.Origen);
        return resultado.Datos;
    }

    private static Historial HistorialDePrueba(int numeroJornadas)
    {
        var historial = new Historial();

        for (int i = 1; i <= numeroJornadas; i++)
        {
            var inicio = new DateTime(2026, 3, i, 8, 0, 0);

            historial.Jornadas.Add(new Jornada
            {
                Numero = i,
                Inicio = inicio,
                Fin = inicio.AddHours(9),
                Trayectos =
                {
                    new Trayecto
                    {
                        Origen = "Córdoba",
                        Destino = "Málaga",
                        Carga = "Aceite «extra»",
                        Kilometros = 162.45,
                        VelocidadMedia = 74.8,
                        VelocidadMaxima = 89.6,
                        Inicio = inicio.AddMinutes(30),
                        Fin = inicio.AddHours(3),
                        FaltaVelocidad = true
                    }
                }
            });
        }

        return historial;
    }
}
