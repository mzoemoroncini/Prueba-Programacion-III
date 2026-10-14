using System.Net;
using System.Text;

public sealed class StubHttpHandler(Func<HttpRequestMessage, Task<string>> respond, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        new(statusCode) { RequestMessage = request, Content = new StringContent(await respond(request), Encoding.UTF8, "application/json") };
}
