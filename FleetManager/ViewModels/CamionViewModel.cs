using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Sección "Camión": datos del camión, el remolque y el encargo leídos del juego.
/// </summary>
public sealed class CamionViewModel : ObjetoObservable, IDisposable
{
    private readonly ServicioTacografo servicio;

    private bool conectado;
    private string camion = "";
    private string matricula = "";
    private string odometro = "";
    private string posicion = "";
    private bool hayRemolque;
    private string remolque = "";
    private bool hayEncargo;
    private string ruta = "";
    private string carga = "";
    private string peso = "";
    private string distancia = "";
    private string entrega = "";

    public CamionViewModel(ServicioTacografo servicio)
    {
        this.servicio = servicio;
        servicio.Actualizado += ServicioActualizado;
        Actualizar();
    }

    public bool Conectado { get => conectado; private set => Asignar(ref conectado, value); }

    /// <summary>Marca y modelo.</summary>
    public string Camion { get => camion; private set => Asignar(ref camion, value); }

    public string Matricula { get => matricula; private set => Asignar(ref matricula, value); }

    public string Odometro { get => odometro; private set => Asignar(ref odometro, value); }

    public string Posicion { get => posicion; private set => Asignar(ref posicion, value); }

    public IndicadorPorcentaje Combustible { get; } = new("Combustible");

    public IndicadorPorcentaje AdBlue { get; } = new("AdBlue");

    public IndicadorPorcentaje DesgasteMotor { get; } = new("Motor");

    public IndicadorPorcentaje DesgasteTransmision { get; } = new("Transmisión");

    public IndicadorPorcentaje DesgasteCabina { get; } = new("Cabina");

    public IndicadorPorcentaje DesgasteChasis { get; } = new("Chasis");

    public IndicadorPorcentaje DesgasteRuedas { get; } = new("Neumáticos");

    public bool HayRemolque { get => hayRemolque; private set => Asignar(ref hayRemolque, value); }

    public string Remolque { get => remolque; private set => Asignar(ref remolque, value); }

    public IndicadorPorcentaje DesgasteRemolque { get; } = new("Desgaste del remolque");

    public bool HayEncargo { get => hayEncargo; private set => Asignar(ref hayEncargo, value); }

    /// <summary>"Madrid → Zaragoza".</summary>
    public string Ruta { get => ruta; private set => Asignar(ref ruta, value); }

    public string Carga { get => carga; private set => Asignar(ref carga, value); }

    public string Peso { get => peso; private set => Asignar(ref peso, value); }

    public IndicadorPorcentaje DanoCarga { get; } = new("Daño de la carga");

    public string Distancia { get => distancia; private set => Asignar(ref distancia, value); }

    public string Entrega { get => entrega; private set => Asignar(ref entrega, value); }

    public void Dispose() => servicio.Actualizado -= ServicioActualizado;

    private void ServicioActualizado(object? sender, EventArgs e) => Actualizar();

    private void Actualizar()
    {
        DatosJuego datos = servicio.Datos;
        Conectado = datos.Conectado;

        if (!datos.Conectado)
        {
            return; // Se mantienen los últimos datos conocidos.
        }

        Camion = $"{datos.Marca} {datos.Modelo}".Trim();
        Matricula = datos.Matricula;
        Odometro = Formato.Numero(datos.Odometro) + " km";
        Posicion = $"X {datos.PosicionX:0} · Y {datos.PosicionY:0} · Z {datos.PosicionZ:0}";

        Combustible.ActualizarDeposito(datos.Combustible, datos.CapacidadCombustible, "L");
        AdBlue.ActualizarDeposito(datos.AdBlue, datos.CapacidadAdBlue, "L");
        DesgasteMotor.ActualizarDesgaste(datos.DesgasteMotor);
        DesgasteTransmision.ActualizarDesgaste(datos.DesgasteTransmision);
        DesgasteCabina.ActualizarDesgaste(datos.DesgasteCabina);
        DesgasteChasis.ActualizarDesgaste(datos.DesgasteChasis);
        DesgasteRuedas.ActualizarDesgaste(datos.DesgasteRuedas);

        HayRemolque = datos.RemolqueEnganchado;
        Remolque = datos.RemolqueEnganchado ? datos.Remolque : "Sin remolque enganchado";
        DesgasteRemolque.ActualizarDesgaste(datos.DesgasteRemolque);

        HayEncargo = datos.Carga.Length > 0;
        Ruta = HayEncargo ? $"{datos.Origen} → {datos.Destino}" : "Sin encargo";
        Carga = datos.Carga;
        Peso = HayEncargo ? Formato.Toneladas(datos.PesoCarga) : "";
        DanoCarga.ActualizarDesgaste(datos.DanoCarga);
        Distancia = HayEncargo ? $"{Formato.Numero(datos.DistanciaPlanificadaKm)} km planificados" : "";
        Entrega = datos.HoraEntrega is { } hora ? Formato.HoraJuego(hora) : "";
    }
}
