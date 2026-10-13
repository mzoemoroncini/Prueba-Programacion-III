using System.Net;
using System.Text;

public sealed class StubHttpHandler(Func<HttpRequestMessage, Task<string>> respond) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        new(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(await respond(request), Encoding.UTF8, "application/json") };
}
