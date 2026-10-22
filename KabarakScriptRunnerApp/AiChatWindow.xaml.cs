using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KabarakScriptRunner.Core.Services;
using KabarakScriptRunner.Data.Repositories;

namespace KabarakScriptRunnerApp
{
    public partial class AiChatWindow : Window
    {
        private readonly ScriptRepository _repository;
        private readonly string _connectionString;
        private readonly string _openAiApiKey; // In production, store this in appsettings.json or Environment Variable
        private readonly HttpClient _httpClient;
        private readonly List<ChatMessage> _messages = new();

        public AiChatWindow(ScriptRepository repository, string connectionString, string openAiApiKey)
        {
            InitializeComponent();
            _repository = repository;
            _connectionString = connectionString;
            _openAiApiKey = openAiApiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            // Add welcome message
            AddMessage("AI", "Hello! Ask me anything about the university data. For example: 'How many students enrolled in 2024?'");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void Send_Click(object sender, RoutedEventArgs e) => await ProcessUserInput();

        private async void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await ProcessUserInput();
        }

        private async Task ProcessUserInput()
        {
            var question = UserInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            UserInput.Clear();
            AddMessage("User", question);

            try
            {
                SendButton.IsEnabled = false;
                AddMessage("AI", "Thinking...");

                // 1. Get Schema (Cached in real app)
                string schema = await GetDatabaseSchemaAsync();

                // 2. Generate SQL
                string sqlQuery = await GenerateSqlAsync(question, schema);

                // 3. Validate SQL (CRITICAL SECURITY STEP)
                var validationResult = ScriptValidator.Validate(sqlQuery);
                if (!validationResult.IsValid)
                {
                    AddMessage("System", $"⚠️ The AI generated an unsafe query, which was blocked: {string.Join(", ", validationResult.Errors)}");
                    return;
                }

                // 4. Execute Query
                var dataTable = await ScriptExecutorService.ExecuteScriptAsync(_connectionString, sqlQuery, null, null);

                // 5. Explain Data
                string explanation = await ExplainDataAsync(question, dataTable);

                // 6. Display result
                AddMessage("AI", explanation);

                // Optional: Add a button to export the DataTable to Excel here
            }
            catch (Exception ex)
            {
                AddMessage("System", $"❌ Error: {ex.Message}");
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private async Task<string> GetDatabaseSchemaAsync()
        {
            // Simplified schema retrieval for clarity. 
            // In a real app, you should query INFORMATION_SCHEMA.COLUMNS and cache this.
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();
            var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA IN ('uni', 'app') 
                ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION", connection);

            var schemaBuilder = new StringBuilder();
            using var reader = await cmd.ExecuteReaderAsync();
            string currentTable = "";
            while (await reader.ReadAsync())
            {
                var table = $"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}";
                if (table != currentTable)
                {
                    schemaBuilder.AppendLine($"\nTable: {table}");
                    currentTable = table;
                }
                schemaBuilder.AppendLine($"  - {reader["COLUMN_NAME"]} ({reader["DATA_TYPE"]})");
            }
            return schemaBuilder.ToString();
        }

        private async Task<string> GenerateSqlAsync(string question, string schema)
        {
            var prompt = $@"
You are a SQL Server expert. Generate a T-SQL SELECT query to answer the user's question.
Use ONLY the following schema. Do not use any other tables.

Schema:
{schema}

Rules:
- Only return the SQL query. No markdown, no explanations.
- Always use schema prefix (e.g., uni.Students).
- Use only SELECT statements.

User Question: {question}
SQL Query:";

            var response = await CallOpenAiAsync(prompt);
            return response.Replace("```sql", "").Replace("```", "").Trim();
        }

        private async Task<string> ExplainDataAsync(string question, DataTable data)
        {
            // Convert DataTable to a readable string (limit rows to save tokens)
            var dataString = new StringBuilder();
            int rowCount = 0;
            foreach (DataRow row in data.Rows)
            {
                if (rowCount++ > 10) { dataString.AppendLine("... (more rows)"); break; }
                dataString.AppendLine(string.Join(", ", row.ItemArray));
            }

            var prompt = $@"
The user asked: '{question}'.
Here is the data returned from the database:
{dataString}

Please explain this data clearly and concisely to the user. 
If it's a number, explain what it represents. 
If it's a list, summarize the key findings.";

            return await CallOpenAiAsync(prompt);
        }

        private async Task<string> CallOpenAiAsync(string prompt)
        {
            var request = new
            {
                model = "gpt-4o-mini", // or gpt-4
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.0
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }

        private void AddMessage(string role, string text)
        {
            // In a real app, use a proper DataTemplate with different styles for User vs AI
            _messages.Add(new ChatMessage { Role = role, Text = text });
            ChatHistory.ItemsSource = null;
            ChatHistory.ItemsSource = _messages;
            ChatHistory.ScrollIntoView(_messages.Last());
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Text { get; set; }
    }
}