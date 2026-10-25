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
        private readonly string _apiKey;
        private readonly string _provider;
        private readonly HttpClient _httpClient;
        private readonly List<ChatMessage> _messages = new();

        public AiChatWindow(ScriptRepository repository, string connectionString, string apiKey, string provider)
        {
            InitializeComponent();
            _repository = repository;
            _connectionString = connectionString;
            _apiKey = apiKey;
            _provider = provider;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

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

            // Create the AI message object with a "Thinking..." placeholder
            var aiMessage = new ChatMessage { Role = "AI", Text = "Thinking..." };
            _messages.Add(aiMessage);
            RefreshChat(); // Update UI to show "Thinking..."

            try
            {
                SendButton.IsEnabled = false;

                // 1. Get Schema
                string schema = await GetDatabaseSchemaAsync();

                // 2. Generate SQL
                string sqlQuery = await GenerateSqlAsync(question, schema);

                // 3. Validate SQL (CRITICAL SECURITY STEP)
                var validationResult = ScriptValidator.Validate(sqlQuery);
                if (!validationResult.IsValid)
                {
                    aiMessage.Text = $"⚠️ The AI generated an unsafe query, which was blocked: {string.Join(", ", validationResult.Errors)}";
                    return;
                }

                // 4. Execute Query
                var dataTable = await ScriptExecutorService.ExecuteScriptAsync(_connectionString, sqlQuery, null, null);

                // 5. Explain Data
                string explanation = await ExplainDataAsync(question, dataTable);

                // 6. Display result (This replaces "Thinking..." in the UI)
                aiMessage.Text = explanation;
            }
            catch (Exception ex)
            {
                aiMessage.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private async Task<string> GetDatabaseSchemaAsync()
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'uni' 
                ORDER BY TABLE_NAME, ORDINAL_POSITION", connection);

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

            var response = await CallAiAsync(prompt);
            return response.Replace("```sql", "").Replace("```", "").Trim();
        }

        private async Task<string> ExplainDataAsync(string question, DataTable data)
        {
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

            return await CallAiAsync(prompt);
        }

        private async Task<string> CallAiAsync(string prompt)
        {
            string endpoint = _provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
                : "https://api.openai.com/v1/chat/completions";

            string model = _provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? "gemini-2.0-flash"
                : "gpt-4o-mini";

            var request = new
            {
                model = model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.0
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"AI API Error ({response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "No response generated.";
        }

        private void AddMessage(string role, string text)
        {
            _messages.Add(new ChatMessage { Role = role, Text = text });
            RefreshChat();
        }

        private void RefreshChat()
        {
            // Build the UI programmatically for the Google AI look
            ChatMessagesPanel.Children.Clear();

            foreach (var msg in _messages)
            {
                var bubble = new Border
                {
                    Background = msg.Role == "User" ? new SolidColorBrush(Color.FromRgb(227, 242, 253)) : Brushes.Transparent,
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = msg.Role == "User" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    MaxWidth = 600
                };

                // If it's the AI or System, don't use a bubble, just plain text
                if (msg.Role == "User")
                {
                    bubble.Child = new TextBlock { Text = msg.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = Brushes.Black };
                }
                else
                {
                    // AI response styling
                    var sp = new StackPanel();
                    var icon = new TextBlock { Text = "✨", Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192)), FontSize = 16, Margin = new Thickness(0, 0, 0, 4) };
                    var tb = new TextBlock { Text = msg.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = Brushes.Black, LineHeight = 20 };
                    sp.Children.Add(icon);
                    sp.Children.Add(tb);
                    bubble.Child = sp;
                }

                ChatMessagesPanel.Children.Add(bubble);
            }

            ChatScrollViewer.ScrollToEnd();
        }

    }

    public class ChatMessage : System.ComponentModel.INotifyPropertyChanged
    {
        private string _text = "";
        public string Role { get; set; } = "";

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Text)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}