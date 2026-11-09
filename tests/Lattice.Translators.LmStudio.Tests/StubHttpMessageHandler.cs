namespace Lattice.Translators.LmStudio.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        ArgumentNullException.ThrowIfNull(responder);
        this.responder = responder;
    }

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add((request.Method, request.RequestUri, body));

        return await responder(request, cancellationToken).ConfigureAwait(false);
    }
}
