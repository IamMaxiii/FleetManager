using System.Globalization;
using FleetManager.MapaJuego;

// Generador del mapa del juego. Lo lanza FleetManager:
//   FleetManager.GeneradorMapa.exe <carpeta del juego> <carpeta de destino> <huella>
//
// Escribe una línea por cada novedad, que FleetManager va leyendo:
//   PROGRESO|<fracción de 0 a 1>|<texto>
//   HECHO
//   ERROR|<mensaje>
// y termina con código 0 si todo ha ido bien.

if (args.Length != 3)
{
    Console.WriteLine("ERROR|Uso: FleetManager.GeneradorMapa <carpeta del juego> <carpeta de destino> <huella>");
    return 2;
}

// En UTF-8, para que los acentos lleguen bien a FleetManager.
Console.OutputEncoding = System.Text.Encoding.UTF8;

// ts-map escribe mucho en la consola; se aparta para que solo salgan nuestras líneas.
TextWriter salida = Console.Out;
Console.SetOut(TextWriter.Null);

try
{
    // El progreso se escribe en el mismo hilo para que las líneas salgan en orden.
    GeneradorMapa.Generar(args[0], args[1], args[2], new ProgresoInmediato(salida), CancellationToken.None);
    salida.WriteLine("HECHO");
    salida.Flush();
    return 0;
}
catch (Exception ex)
{
    salida.WriteLine($"ERROR|{ex.Message.ReplaceLineEndings(" ")}");
    salida.Flush();
    return 1;
}

/// <summary>Escribe el progreso al momento (Progress&lt;T&gt; lo haría en otro hilo y desordenado).</summary>
internal sealed class ProgresoInmediato(TextWriter salida) : IProgress<ProgresoMapa>
{
    public void Report(ProgresoMapa valor)
    {
        salida.WriteLine(string.Create(CultureInfo.InvariantCulture, $"PROGRESO|{valor.Fraccion:0.0000}|{valor.Texto}"));
        salida.Flush();
    }
}
