using System.Globalization;
using System.IO;
using System.Text;

namespace FleetManager.Storage.Importacion;

/// <summary>
/// Lee los archivos .dat antiguos, que guardan un valor por línea.
/// Si un valor falta o no tiene el tipo esperado, lanza
/// <see cref="FormatoAntiguoException"/> con el número de línea.
/// </summary>
internal sealed class LectorLineas
{
    private readonly TextReader lector;

    public LectorLineas(TextReader lector)
    {
        this.lector = lector;
    }

    public int NumeroLinea { get; private set; }

    /// <param name="que">Qué se esperaba leer, para el mensaje de error.</param>
    public string Leer(string que)
    {
        string? linea = lector.ReadLine();
        NumeroLinea++;

        return linea ?? throw new FormatoAntiguoException(
            $"el archivo se acaba antes de tiempo (faltaba {que}).", NumeroLinea);
    }

    public int LeerEntero(string que)
    {
        string texto = Leer(que);

        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valor)
            ? valor
            : throw ValorIncorrecto(que, texto);
    }

    /// <summary>Lee un número entero que no puede ser negativo (por ejemplo, una cantidad).</summary>
    public int LeerCantidad(string que)
    {
        int valor = LeerEntero(que);

        return valor >= 0
            ? valor
            : throw new FormatoAntiguoException($"{que} no puede ser negativo ({valor}).", NumeroLinea);
    }

    public double LeerDecimal(string que)
    {
        string texto = Leer(que);

        return double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double valor)
               && double.IsFinite(valor)
            ? valor
            : throw ValorIncorrecto(que, texto);
    }

    /// <summary>Lee "1" (sí) o "0" (no).</summary>
    public bool LeerSiNo(string que)
    {
        string texto = Leer(que);

        return texto switch
        {
            "1" => true,
            "0" => false,
            _ => throw ValorIncorrecto(que, texto)
        };
    }

    /// <summary>
    /// Lee una fecha guardada como "ticks" (número de intervalos de 100 ns desde el año 1).
    /// El formato antiguo usaba 0 o -1 para "sin fecha"; en ese caso devuelve vacío.
    /// </summary>
    public DateTime? LeerFecha(string que)
    {
        string texto = Leer(que);

        if (!long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
        {
            throw ValorIncorrecto(que, texto);
        }

        if (ticks is 0 or -1)
        {
            return null;
        }

        if (ticks < 0 || ticks > DateTime.MaxValue.Ticks)
        {
            throw ValorIncorrecto(que, texto);
        }

        return new DateTime(ticks, DateTimeKind.Unspecified);
    }

    /// <summary>Lee un texto codificado en Base64 (UTF-8), como guardaba la versión antigua.</summary>
    public string LeerTexto(string que)
    {
        string texto = Leer(que);

        if (texto.Length == 0)
        {
            return "";
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(texto));
        }
        catch (FormatException)
        {
            throw ValorIncorrecto(que, texto);
        }
    }

    private FormatoAntiguoException ValorIncorrecto(string que, string texto)
    {
        string muestra = texto.Length > 40 ? texto[..40] + "…" : texto;
        return new FormatoAntiguoException($"valor no válido para {que}: \"{muestra}\".", NumeroLinea);
    }
}
