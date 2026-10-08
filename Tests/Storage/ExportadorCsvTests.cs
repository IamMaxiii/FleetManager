using System.Text;
using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Storage;

/// <summary>
/// CSV para Excel en español: ";" como separador, coma decimal, UTF-8 con BOM.
/// </summary>
public sealed class ExportadorCsvTests : IDisposable
{
    private readonly CarpetaTemporal carpeta = new();

    public void Dispose() => carpeta.Dispose();

    private static string[] Lineas(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void La_cabecera_tiene_las_columnas_elegidas()
    {
        string csv = ExportadorCsv.Generar([]);

        Assert.Equal(
            "Jornada;Día;Origen;Destino;Carga;Kilómetros;Velocidad media;Velocidad máxima;Inicio;Fin;Falta de velocidad;Falta de conducción",
            Lineas(csv)[0]);
    }

    [Fact]
    public void Cada_trayecto_es_una_fila_con_coma_decimal()
    {
        Historial historial = EjemplosHistorial.DosJornadas();
        Jornada jornada = historial.Jornadas[0];

        string csv = ExportadorCsv.Generar([(jornada, jornada.Trayectos[1])]);

        Assert.Equal("1;Día 176 (lun);Zaragoza;Barcelona;Maquinaria;296,0;98,7;109,0;11:00;14:00;Sí;No", Lineas(csv)[1]);
    }

    [Fact]
    public void Si_el_trayecto_acaba_otro_dia_el_fin_lleva_el_dia()
    {
        var jornada = new Jornada { Numero = 3 };
        Trayecto nocturno = EjemplosHistorial.Trayecto(176, 23, 2, "Lyon", "París", 180);

        string fila = Lineas(ExportadorCsv.Generar([(jornada, nocturno)]))[1];

        Assert.Contains(";23:00;Día 177 (mar) 01:00;", fila);
    }

    [Fact]
    public void Los_textos_con_punto_y_coma_o_comillas_van_entre_comillas()
    {
        var jornada = new Jornada { Numero = 1 };
        Trayecto trayecto = EjemplosHistorial.Trayecto(176, 7, 1, "Madrid", "Toledo", 70);
        trayecto.Carga = "Vino \"reserva\"; 20 t";

        string fila = Lineas(ExportadorCsv.Generar([(jornada, trayecto)]))[1];

        Assert.Contains(";\"Vino \"\"reserva\"\"; 20 t\";", fila);
    }

    [Fact]
    public void El_archivo_es_UTF8_con_BOM_para_que_Excel_lea_los_acentos()
    {
        string ruta = carpeta.Archivo("trayectos.csv");

        ExportadorCsv.Guardar(ruta, "Día;Perpiñán\r\n");

        byte[] bytes = File.ReadAllBytes(ruta);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("Día;Perpiñán\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }
}
