namespace CatalogoApi.Tests.Suporte;

/// <summary>TimeProvider controlado pelo teste.</summary>
public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = agora;

    public override DateTimeOffset GetUtcNow() => Agora;

    public void Avancar(TimeSpan intervalo) => Agora = Agora.Add(intervalo);
}
