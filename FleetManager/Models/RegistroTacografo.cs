namespace FleetManager.Models;

/// <summary>
/// El registro del tacógrafo: la lista de periodos de actividad, en orden y sin
/// huecos. Todos los contadores (conducción diaria, semanal, pausas...) se
/// calculan a partir de aquí. Se guarda en tacografo.json.
/// </summary>
public sealed class RegistroTacografo
{
    public List<PeriodoActividad> Periodos { get; set; } = [];
}
