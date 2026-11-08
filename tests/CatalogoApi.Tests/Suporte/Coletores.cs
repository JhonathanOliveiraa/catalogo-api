using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CatalogoApi.Tests.Suporte;

/// <summary>Coleção segura para o exporter em memória, que escreve de várias threads.</summary>
public sealed class AtividadesColetadas : ICollection<Activity>
{
    private readonly List<Activity> _itens = [];
    private readonly Lock _trava = new();

    public int Count { get { lock (_trava) { return _itens.Count; } } }
    public bool IsReadOnly => false;

    public Activity[] Instantaneo() { lock (_trava) { return [.. _itens]; } }

    public void Add(Activity item) { lock (_trava) { _itens.Add(item); } }
    public void Clear() { lock (_trava) { _itens.Clear(); } }
    public bool Contains(Activity item) { lock (_trava) { return _itens.Contains(item); } }
    public void CopyTo(Activity[] array, int arrayIndex) { lock (_trava) { _itens.CopyTo(array, arrayIndex); } }
    public bool Remove(Activity item) { lock (_trava) { return _itens.Remove(item); } }
    public IEnumerator<Activity> GetEnumerator() => ((IEnumerable<Activity>)Instantaneo()).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Logger provider que guarda as entradas para inspeção.</summary>
public sealed class ColetorLogs : ILoggerProvider
{
    private readonly List<(LogLevel Nivel, string Categoria, string Mensagem, Exception? Excecao)> _entradas = [];
    private readonly Lock _trava = new();

    public IReadOnlyList<(LogLevel Nivel, string Categoria, string Mensagem, Exception? Excecao)> Entradas
    {
        get { lock (_trava) { return [.. _entradas]; } }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(ColetorLogs dono, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (dono._trava)
            {
                dono._entradas.Add((logLevel, categoria, formatter(state, exception), exception));
            }
        }
    }
}
