using Microsoft.AspNetCore.Hosting;

namespace MesaAyuda.Pruebas.Integracion;

/// <summary>
/// Variante de la fábrica con la verificación de firma activada, tal como debe
/// quedar configurado el servicio en un despliegue real.
/// </summary>
public sealed class FabricaConFirma : FabricaPruebas
{
    public const string Secreto = "secreto-de-pruebas-de-integracion";

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        base.ConfigureWebHost(constructor);

        constructor.UseSetting("Orquestacion:ExigirFirma", "true");
        constructor.UseSetting("Orquestacion:SecretoWebhook", Secreto);
    }
}
