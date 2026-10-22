using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KabarakScriptRunner.Core.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _provider;
        private readonly string _model;

        public AiService(string apiKey, string provider = "Gemini")
        {
            _apiKey = apiKey;
            _provider = provider;
            _httpClient = new HttpClient();

            // Set the model based on the provider
            _model = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? "gemini-2.0-flash"
                : "gpt-4o-mini";
        }

        // STEP 1: Generate SQL (Non-Streaming)
        public async Task<string> GenerateSqlAsync(string userQuestion, string schema, CancellationToken token)
        {
            var systemPrompt = $@"You are a T-SQL expert. Generate a single SELECT query for SQL Server.
Use ONLY the tables and columns provided in this schema:
{schema}
Rules:
- Only output the raw SQL query. No markdown, no explanations.
- Never use DELETE, UPDATE, INSERT, DROP, or ALTER.
- Always use the 'uni.' schema prefix.

User Question: {userQuestion}
SQL:";

            var responseText = await CallAiAsync(systemPrompt, token, stream: false);
            return responseText.Replace("```sql", "").Replace("```", "").Trim();
        }

        // STEP 2: Explain Data (Streaming)
        public async IAsyncEnumerable<string> ExplainDataStreamingAsync(
            string userQuestion,
            DataTable data,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            // Convert DataTable to a compact JSON string
            var dataString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
            if (dataString.Length > 8000) dataString = dataString.Substring(0, 8000) + "...(truncated)";

            var prompt = $@"The user asked: '{userQuestion}'.
Here is the JSON data returned from the database:
{dataString}

Explain this data clearly and concisely. If it's a list, summarize the key findings.";

            await foreach (var chunk in CallAiStreamingAsync(prompt, token))
            {
                yield return chunk;
            }
        }

        // HELPER: Handle the HTTP call for both providers
        private async Task<string> CallAiAsync(string prompt, CancellationToken token, bool stream)
        {
            var request = BuildRequest(prompt);
            var response = await _httpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);

            // Extract text based on provider response structure
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        // HELPER: Handle Streaming for both providers
        private async IAsyncEnumerable<string> CallAiStreamingAsync(string prompt, CancellationToken token)
        {
            var request = BuildRequest(prompt, stream: true);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            using var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync(token));

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6); // Remove "data: "
                if (data == "[DONE]") break;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var delta = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var content))
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrEmpty(text)) yield return text;
                    }
                }
                catch { /* Ignore malformed chunks */ }
            }
        }

        private HttpRequestMessage BuildRequest(string prompt, bool stream = false)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, GetEndpoint());
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var body = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
                stream = stream,
                temperature = 0.0
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            return request;
        }

        private string GetEndpoint()
        {
            return _provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
                : "https://api.openai.com/v1/chat/completions";
        }
    }
}