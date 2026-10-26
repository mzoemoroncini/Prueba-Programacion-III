using System.Net;
using System.Text;

public sealed class StubHttpHandler(Func<HttpRequestMessage, Task<string>> respond, HttpStatusCode statusCode = HttpStatusCode.OK, TimeSpan? retryAfter = null) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode) { RequestMessage = request, Content = new StringContent(await respond(request), Encoding.UTF8, "application/json") };
        if (retryAfter.HasValue) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
        return response;
    }
}
