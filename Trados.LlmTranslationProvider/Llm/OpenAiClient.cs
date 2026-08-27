using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Trados.LlmTranslationProvider.Llm
{
    /// <summary>
    /// First <see cref="ILlmClient"/> implementation, calling OpenAI's Chat Completions endpoint.
    /// A single static HttpClient is reused across requests, per .NET best practice (creating a
    /// new HttpClient per call can exhaust sockets under load).
    /// </summary>
    public class OpenAiClient : ILlmClient
    {
        private const string EndpointUrl = "https://api.openai.com/v1/chat/completions";

        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public async Task<string> TranslateAsync(LlmTranslationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.ApiKey))
            {
                throw new InvalidOperationException(
                    "No OpenAI API key configured for this provider. Open the provider's settings in Trados Studio and enter one.");
            }

            var payload = new JObject
            {
                ["model"] = request.Model,
                ["temperature"] = 0.2,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                    new JObject { ["role"] = "user", ["content"] = request.UserPrompt }
                }
            };

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, EndpointUrl))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
                httpRequest.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await HttpClient.SendAsync(httpRequest).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMessage = TryExtractErrorMessage(body) ?? body;
                        throw new InvalidOperationException(
                            $"OpenAI request failed ({(int)response.StatusCode} {response.StatusCode}): {errorMessage}");
                    }

                    return ExtractCompletionText(body);
                }
            }
        }

        private static string ExtractCompletionText(string responseBody)
        {
            var json = JObject.Parse(responseBody);
            var content = (string)json["choices"]?[0]?["message"]?["content"];
            return (content ?? string.Empty).Trim();
        }

        private static string TryExtractErrorMessage(string responseBody)
        {
            try
            {
                var json = JObject.Parse(responseBody);
                return (string)json["error"]?["message"];
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
