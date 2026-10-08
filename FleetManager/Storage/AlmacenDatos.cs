using System.IO;
using FleetManager.Models;

namespace FleetManager.Storage;

/// <summary>
/// Qué archivos tienen cambios pendientes de guardar.
/// </summary>
[Flags]
public enum ArchivosDatos
{
    Ninguno = 0,
    Historial = 1,
    Perfil = 2,
    Ajustes = 4,
    Tacografo = 8,
    Recorrido = 16,
    Todos = Historial | Perfil | Ajustes | Tacografo | Recorrido
}

/// <summary>
/// Punto único de acceso a los datos guardados: historial, perfil, ajustes,
/// registro del tacógrafo y recorrido de la jornada actual.
/// Quien cambia algo llama a <see cref="MarcarCambios"/>; el guardado se hace
/// después, todo junto, con <see cref="GuardarCambiosPendientes"/>.
///
/// Se usa siempre desde el hilo de la interfaz, el mismo que modifica los datos,
/// para que nunca se guarde un dato a medio cambiar.
/// </summary>
public sealed class AlmacenDatos
{
    private readonly ArchivoJson<Historial> archivoHistorial;
    private readonly ArchivoJson<PerfilConductor> archivoPerfil;
    private readonly ArchivoJson<AjustesApp> archivoAjustes;
    private readonly ArchivoJson<RegistroTacografo> archivoTacografo;
    private readonly RutasDatos rutas;
    private readonly Registro registro;
    private ArchivoJson<MapaRecorrido>? archivoRecorrido;
    private readonly TimeProvider reloj;
    private readonly List<string> avisosCarga = [];

    private ArchivosDatos pendientes;

    public AlmacenDatos(RutasDatos rutas, Registro registro, TimeProvider reloj)
    {
        this.rutas = rutas;
        this.registro = registro;
        this.reloj = reloj;

        archivoHistorial = new ArchivoJson<Historial>(rutas.Historial, rutas.CarpetaCopias, registro, reloj);
        archivoPerfil = new ArchivoJson<PerfilConductor>(rutas.Perfil, rutas.CarpetaCopias, registro, reloj);
        archivoAjustes = new ArchivoJson<AjustesApp>(rutas.Ajustes, rutas.CarpetaCopias, registro, reloj);
        archivoTacografo = new ArchivoJson<RegistroTacografo>(rutas.Tacografo, rutas.CarpetaCopias, registro, reloj);
    }

    /// <summary>Se lanza cada vez que cambia algo que la interfaz puede querer mostrar.</summary>
    public event EventHandler? EstadoCambiado;

    public Historial Historial { get; private set; } = new();

    public PerfilConductor Perfil { get; private set; } = new();

    public AjustesApp Ajustes { get; private set; } = new();

    /// <summary>Registro de actividades del tacógrafo.</summary>
    public RegistroTacografo Tacografo { get; private set; } = new();

    /// <summary>Recorrido de la jornada indicada en <see cref="RecorridoDe"/> (la abierta, o la última).</summary>
    public MapaRecorrido Recorrido { get; private set; } = new();

    /// <summary>Jornada a la que pertenece <see cref="Recorrido"/>; vacío si ninguna.</summary>
    public Guid? RecorridoDe { get; private set; }

    /// <summary>Verdadero si no había historial guardado (primera vez que se usa la aplicación).</summary>
    public bool HistorialEsNuevo { get; private set; }

    /// <summary>Verdadero si no había perfil guardado.</summary>
    public bool PerfilEsNuevo { get; private set; }

    /// <summary>Problemas encontrados al cargar, para mostrárselos al usuario.</summary>
    public IReadOnlyList<string> AvisosCarga => avisosCarga;

    public bool HayCambiosPendientes => pendientes != ArchivosDatos.Ninguno;

    /// <summary>Momento del primer cambio aún sin guardar; vacío si está todo guardado.</summary>
    public DateTimeOffset? PrimerCambioPendiente { get; private set; }

    public DateTimeOffset? UltimoGuardado { get; private set; }

    /// <summary>Motivo del último fallo al guardar; vacío si el último guardado fue bien.</summary>
    public string? UltimoError { get; private set; }

    public void Cargar()
    {
        avisosCarga.Clear();
        pendientes = ArchivosDatos.Ninguno;
        PrimerCambioPendiente = null;

        Historial = Cargar(archivoHistorial, ArchivosDatos.Historial, out bool historialNuevo);
        HistorialEsNuevo = historialNuevo;

        Perfil = Cargar(archivoPerfil, ArchivosDatos.Perfil, out bool perfilNuevo);
        PerfilEsNuevo = perfilNuevo;

        Ajustes = Cargar(archivoAjustes, ArchivosDatos.Ajustes, out _);
        Tacografo = Cargar(archivoTacografo, ArchivosDatos.Tacografo, out _);

        registro.Info(
            $"Datos cargados: {Historial.Jornadas.Count} jornadas, {Historial.NumeroTrayectos} trayectos, " +
            $"{Tacografo.Periodos.Count} periodos en el tacógrafo.");

        EstadoCambiado?.Invoke(this, EventArgs.Empty);
    }

    public void MarcarCambios(ArchivosDatos cuales)
    {
        if (cuales == ArchivosDatos.Ninguno)
        {
            return;
        }

        if (pendientes == ArchivosDatos.Ninguno)
        {
            PrimerCambioPendiente = reloj.GetUtcNow();
        }

        pendientes |= cuales;
        EstadoCambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pasa a usar el recorrido de una jornada (lo carga de disco, o lo empieza vacío si
    /// no existe). Antes guarda lo pendiente del recorrido anterior.
    /// </summary>
    public void UsarRecorrido(Guid jornada)
    {
        if (RecorridoDe == jornada)
        {
            return;
        }

        if (pendientes.HasFlag(ArchivosDatos.Recorrido))
        {
            GuardarCambiosPendientes();
        }

        archivoRecorrido = ArchivoRecorrido(jornada);
        Recorrido = LeerSinAvisos(archivoRecorrido);
        RecorridoDe = jornada;
    }

    /// <summary>Recorrido de cualquier jornada (para el historial). Vacío si no tiene.</summary>
    public MapaRecorrido LeerRecorrido(Guid jornada) =>
        jornada == RecorridoDe ? Recorrido : LeerSinAvisos(ArchivoRecorrido(jornada));

    /// <summary>Sustituye todo el historial (por ejemplo, al importar los datos antiguos).</summary>
    public void ReemplazarHistorial(Historial nuevo)
    {
        Historial = nuevo;
        HistorialEsNuevo = false;
        MarcarCambios(ArchivosDatos.Historial);
    }

    public void ReemplazarPerfil(PerfilConductor nuevo)
    {
        Perfil = nuevo;
        PerfilEsNuevo = false;
        MarcarCambios(ArchivosDatos.Perfil);
    }

    /// <summary>
    /// Copia de seguridad del historial tal como está ahora (por ejemplo, antes de borrar).
    /// </summary>
    /// <returns>Ruta de la copia, o vacío si no se pudo hacer (en ese caso no se debe borrar nada).</returns>
    public string? CopiarHistorial(string motivo)
    {
        try
        {
            string ruta = archivoHistorial.GuardarCopia(Historial, motivo);
            registro.Info($"Copia del historial ({motivo}): {ruta}");
            return ruta;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            registro.Error($"No se pudo hacer la copia del historial ({motivo})", ex);
            return null;
        }
    }

    /// <summary>
    /// Guarda los archivos con cambios. Si alguno falla, sigue pendiente y se
    /// reintentará en el siguiente guardado automático.
    /// </summary>
    /// <returns>Verdadero si todo quedó guardado.</returns>
    public bool GuardarCambiosPendientes()
    {
        if (pendientes == ArchivosDatos.Ninguno)
        {
            return true;
        }

        var fallidos = ArchivosDatos.Ninguno;
        string? error = null;

        GuardarSiToca(archivoHistorial, Historial, ArchivosDatos.Historial, ref fallidos, ref error);
        GuardarSiToca(archivoPerfil, Perfil, ArchivosDatos.Perfil, ref fallidos, ref error);
        GuardarSiToca(archivoAjustes, Ajustes, ArchivosDatos.Ajustes, ref fallidos, ref error);
        GuardarSiToca(archivoTacografo, Tacografo, ArchivosDatos.Tacografo, ref fallidos, ref error);

        if (archivoRecorrido is not null)
        {
            GuardarSiToca(archivoRecorrido, Recorrido, ArchivosDatos.Recorrido, ref fallidos, ref error);
        }
        else
        {
            pendientes &= ~ArchivosDatos.Recorrido;
        }

        pendientes = fallidos;

        if (fallidos == ArchivosDatos.Ninguno)
        {
            PrimerCambioPendiente = null;
            UltimoGuardado = reloj.GetUtcNow();
            UltimoError = null;
        }
        else
        {
            // Se vuelve a contar el tiempo de espera antes de reintentar.
            PrimerCambioPendiente = reloj.GetUtcNow();
            UltimoError = error;
        }

        EstadoCambiado?.Invoke(this, EventArgs.Empty);
        return fallidos == ArchivosDatos.Ninguno;
    }

    /// <summary>Archivo del recorrido de una jornada: compacto y sin copias diarias (hay uno por jornada).</summary>
    private ArchivoJson<MapaRecorrido> ArchivoRecorrido(Guid jornada) =>
        new(rutas.Recorrido(jornada), rutas.CarpetaCopias, registro, reloj, compacto: true, copiasDiarias: false);

    /// <summary>Los problemas de un recorrido solo se anotan: perder un dibujo no es grave.</summary>
    private MapaRecorrido LeerSinAvisos(ArchivoJson<MapaRecorrido> archivo)
    {
        var resultado = archivo.Leer();

        if (resultado.Aviso is not null)
        {
            registro.Aviso(resultado.Aviso);
        }

        return resultado.Datos;
    }

    private TDatos Cargar<TDatos>(ArchivoJson<TDatos> archivo, ArchivosDatos cual, out bool esNuevo)
        where TDatos : class, new()
    {
        var resultado = archivo.Leer();

        if (resultado.Aviso is not null)
        {
            avisosCarga.Add(resultado.Aviso);
            registro.Aviso(resultado.Aviso);
        }

        if (resultado.Origen is OrigenDatos.CopiaAnterior or OrigenDatos.CopiaDiaria)
        {
            // Datos recuperados de una copia: se vuelven a guardar para rehacer el archivo principal.
            MarcarCambios(cual);
        }

        esNuevo = resultado.Origen == OrigenDatos.Nuevo && resultado.Aviso is null;
        return resultado.Datos;
    }

    private void GuardarSiToca<TDatos>(
        ArchivoJson<TDatos> archivo,
        TDatos datos,
        ArchivosDatos cual,
        ref ArchivosDatos fallidos,
        ref string? error)
        where TDatos : class, new()
    {
        if (!pendientes.HasFlag(cual))
        {
            return;
        }

        try
        {
            archivo.Guardar(datos);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            fallidos |= cual;
            error = $"{archivo.NombreArchivo}: {ex.Message}";
            registro.Error($"No se pudo guardar {archivo.Ruta}", ex);
        }
    }
}
