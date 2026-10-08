namespace FleetManager.Storage.Importacion;

/// <summary>
/// El archivo de la versión antigua no tiene el formato esperado.
/// Indica la línea donde se encontró el problema.
/// </summary>
public sealed class FormatoAntiguoException : Exception
{
    public FormatoAntiguoException(string mensaje, int linea)
        : base($"Línea {linea}: {mensaje}")
    {
        Linea = linea;
    }

    public int Linea { get; }
}
