using System.Text.RegularExpressions;
using FleetManager.Core;

namespace FleetManager.Tests.Core;

/// <summary>
/// Números de permiso y ADR inventados para el perfil del conductor.
/// </summary>
public class GeneradorDocumentosTests
{
    [Fact]
    public void El_permiso_es_ES_y_6_cifras()
    {
        for (int semilla = 0; semilla < 50; semilla++)
        {
            Assert.Matches(new Regex(@"^ES-\d{6}$"), GeneradorDocumentos.NumeroPermiso(new Random(semilla)));
        }
    }

    [Fact]
    public void El_ADR_es_ADR_y_6_cifras()
    {
        for (int semilla = 0; semilla < 50; semilla++)
        {
            Assert.Matches(new Regex(@"^ADR-\d{6}$"), GeneradorDocumentos.NumeroAdr(new Random(semilla)));
        }
    }
}
