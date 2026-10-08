using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Storage;

/// <summary>
/// Pruebas del almacén de datos y del guardado automático.
/// </summary>
public sealed class AlmacenDatosTests : IDisposable
{
    private readonly CarpetaTemporal carpeta = new();
    private readonly RelojFalso reloj = new();
    private readonly RutasDatos rutas;

    public AlmacenDatosTests()
    {
        rutas = new RutasDatos(carpeta.Ruta);
    }

    public void Dispose() => carpeta.Dispose();

    [Fact]
    public void La_primera_vez_el_historial_es_nuevo_y_no_hay_avisos()
    {
        AlmacenDatos almacen = CrearAlmacen();

        almacen.Cargar();

        Assert.True(almacen.HistorialEsNuevo);
        Assert.True(almacen.PerfilEsNuevo);
        Assert.Empty(almacen.AvisosCarga);
        Assert.False(almacen.HayCambiosPendientes);
    }

    [Fact]
    public void Los_cambios_guardados_siguen_ahi_al_volver_a_abrir()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();

        almacen.Historial.Jornadas.Add(new Jornada { Numero = 1, Inicio = new DateTime(2026, 3, 1, 8, 0, 0) });
        almacen.Perfil.Nombre = "Ana";
        almacen.MarcarCambios(ArchivosDatos.Historial | ArchivosDatos.Perfil);

        Assert.True(almacen.GuardarCambiosPendientes());
        Assert.False(almacen.HayCambiosPendientes);
        Assert.NotNull(almacen.UltimoGuardado);

        AlmacenDatos reabierto = CrearAlmacen();
        reabierto.Cargar();

        Assert.False(reabierto.HistorialEsNuevo);
        Assert.Single(reabierto.Historial.Jornadas);
        Assert.Equal("Ana", reabierto.Perfil.Nombre);
    }

    [Fact]
    public void El_registro_del_tacografo_se_guarda_y_se_recupera()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();

        var inicio = new DateTime(2026, 3, 2, 8, 0, 0);
        almacen.Tacografo.Periodos.Add(new PeriodoActividad { Actividad = Actividad.Conduccion, Inicio = inicio, Fin = inicio.AddHours(2) });
        almacen.MarcarCambios(ArchivosDatos.Tacografo);
        almacen.GuardarCambiosPendientes();

        AlmacenDatos reabierto = CrearAlmacen();
        reabierto.Cargar();

        PeriodoActividad periodo = Assert.Single(reabierto.Tacografo.Periodos);
        Assert.Equal(Actividad.Conduccion, periodo.Actividad);
        Assert.Equal(inicio.AddHours(2), periodo.Fin);
        Assert.Contains("\"Conduccion\"", File.ReadAllText(rutas.Tacografo));
    }

    [Fact]
    public void La_copia_del_historial_se_puede_leer_y_no_cuenta_como_copia_diaria()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();
        almacen.ReemplazarHistorial(EjemplosHistorial.DosJornadas());

        string? copia = almacen.CopiarHistorial("antes-de-borrar");

        Assert.NotNull(copia);
        Assert.StartsWith(Path.Combine(rutas.CarpetaCopias, "historial-antes-de-borrar-"), copia);

        var lector = new ArchivoJson<Historial>(copia!, carpeta.Archivo("otra"), new Registro(rutas.Registro), reloj);
        Assert.Equal(2, lector.Leer().Datos.Jornadas.Count);

        // Si el historial principal se estropea, no se recupera de esta copia sino de las diarias.
        File.WriteAllText(rutas.Historial, "dañado");
        AlmacenDatos reabierto = CrearAlmacen();
        reabierto.Cargar();
        Assert.Empty(reabierto.Historial.Jornadas);
    }

    [Fact]
    public void Solo_se_guardan_los_archivos_con_cambios()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();

        almacen.MarcarCambios(ArchivosDatos.Ajustes);
        almacen.GuardarCambiosPendientes();

        Assert.True(File.Exists(rutas.Ajustes));
        Assert.False(File.Exists(rutas.Historial));
        Assert.False(File.Exists(rutas.Perfil));
    }

    [Fact]
    public void El_guardado_automatico_espera_30_segundos_desde_el_primer_cambio()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();
        var guardado = new GuardadoAutomatico(almacen, reloj, GuardadoAutomatico.EsperaPorDefecto);

        almacen.MarcarCambios(ArchivosDatos.Historial);
        reloj.Avanzar(TimeSpan.FromSeconds(20));
        almacen.MarcarCambios(ArchivosDatos.Historial); // otro cambio: no reinicia la espera

        Assert.False(guardado.Comprobar());
        Assert.False(File.Exists(rutas.Historial));

        reloj.Avanzar(TimeSpan.FromSeconds(10));

        Assert.True(guardado.Comprobar());
        Assert.True(File.Exists(rutas.Historial));
        Assert.False(almacen.HayCambiosPendientes);
    }

    [Fact]
    public void El_guardado_automatico_no_hace_nada_sin_cambios()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();
        var guardado = new GuardadoAutomatico(almacen, reloj, GuardadoAutomatico.EsperaPorDefecto);

        reloj.Avanzar(TimeSpan.FromMinutes(5));

        Assert.False(guardado.Comprobar());
        Assert.False(File.Exists(rutas.Historial));
    }

    [Fact]
    public void Los_datos_recuperados_de_una_copia_quedan_pendientes_para_rehacer_el_archivo()
    {
        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();
        almacen.Historial.Jornadas.Add(new Jornada { Numero = 1, Inicio = new DateTime(2026, 3, 1, 8, 0, 0) });
        almacen.MarcarCambios(ArchivosDatos.Historial);
        almacen.GuardarCambiosPendientes();
        almacen.MarcarCambios(ArchivosDatos.Historial);
        almacen.GuardarCambiosPendientes(); // el segundo guardado crea historial.json.bak
        File.WriteAllText(rutas.Historial, "dañado");

        AlmacenDatos reabierto = CrearAlmacen();
        reabierto.Cargar();

        Assert.Single(reabierto.Historial.Jornadas);
        Assert.Single(reabierto.AvisosCarga);
        Assert.False(reabierto.HistorialEsNuevo);
        Assert.True(reabierto.HayCambiosPendientes);

        Assert.True(reabierto.GuardarCambiosPendientes());
        Assert.True(File.Exists(rutas.Historial));
    }

    [Fact]
    public void Si_no_se_puede_guardar_los_cambios_siguen_pendientes_y_se_informa()
    {
        File.WriteAllText(rutas.Historial, "{ \"Version\": 99, \"Datos\": { \"Jornadas\": [] } }");

        AlmacenDatos almacen = CrearAlmacen();
        almacen.Cargar();
        almacen.MarcarCambios(ArchivosDatos.Historial);

        Assert.False(almacen.GuardarCambiosPendientes());
        Assert.True(almacen.HayCambiosPendientes);
        Assert.Contains("historial.json", almacen.UltimoError);
        Assert.False(almacen.HistorialEsNuevo);
    }

    private AlmacenDatos CrearAlmacen() =>
        new(rutas, new Registro(rutas.Registro), reloj);
}
