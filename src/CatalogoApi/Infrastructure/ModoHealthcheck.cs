namespace CatalogoApi.Infrastructure;

/// <summary>
/// <c>dotnet CatalogoApi.dll --healthcheck</c>: usado pelo HEALTHCHECK da imagem chiseled,
/// que não tem shell nem curl (AD-11). Não sobe servidor nem roda migrations.
/// </summary>
public static class ModoHealthcheck
{
    public const string Argumento = "--healthcheck";
    private static readonly Uri Liveness = new("http://localhost:8080/health/live");

    public static async Task<int> ExecutarAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var resposta = await http.GetAsync(Liveness);
            return resposta.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            await Console.Error.WriteLineAsync($"healthcheck falhou: {e.Message}");
            return 1;
        }
    }
}
