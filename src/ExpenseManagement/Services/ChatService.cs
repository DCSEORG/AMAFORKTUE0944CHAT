using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
    bool IsEnabled { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;
    private OpenAIClient? _client;

    public bool IsEnabled => !string.IsNullOrEmpty(_configuration["OpenAI:Endpoint"]) 
                            && _configuration.GetValue<bool>("GenAI:Enabled");

    public ChatService(IConfiguration configuration, IExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
        InitializeClient();
    }

    private void InitializeClient()
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        if (string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("OpenAI endpoint not configured. Chat functionality will return demo responses.");
            return;
        }

        try
        {
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            _client = new OpenAIClient(new Uri(endpoint), credential);
            _logger.LogInformation("OpenAI client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenAI client");
        }
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        if (!IsEnabled || _client == null)
        {
            return GetDemoResponse(request.Message);
        }

        try
        {
            var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";
            
            // Build chat messages
            var chatMessages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(GetSystemPrompt())
            };

            // Add history
            foreach (var msg in request.History)
            {
                if (msg.Role == "user")
                    chatMessages.Add(new ChatRequestUserMessage(msg.Content));
                else if (msg.Role == "assistant")
                    chatMessages.Add(new ChatRequestAssistantMessage(msg.Content));
            }

            // Add current message
            chatMessages.Add(new ChatRequestUserMessage(request.Message));

            // Define available functions for function calling
            var options = new ChatCompletionsOptions(deploymentName, chatMessages)
            {
                Tools = {
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_expenses",
                        Description = "Get list of expenses with optional filters",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                search_term = new { type = "string", description = "Search term to filter expenses" },
                                status = new { type = "string", description = "Filter by status: Draft, Submitted, Approved, Rejected" }
                            }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_pending_expenses",
                        Description = "Get expenses pending approval"
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_dashboard_stats",
                        Description = "Get dashboard statistics including total expenses, pending approvals, and approved amounts"
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "create_expense",
                        Description = "Create a new expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                amount = new { type = "number", description = "Amount in GBP" },
                                category = new { type = "string", description = "Category: Travel, Meals, Supplies, Accommodation, Other" },
                                date = new { type = "string", description = "Expense date in YYYY-MM-DD format" },
                                description = new { type = "string", description = "Description of the expense" }
                            },
                            required = new[] { "amount", "category", "date" }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "approve_expense",
                        Description = "Approve a pending expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expense_id = new { type = "integer", description = "ID of the expense to approve" }
                            },
                            required = new[] { "expense_id" }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "reject_expense",
                        Description = "Reject a pending expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expense_id = new { type = "integer", description = "ID of the expense to reject" }
                            },
                            required = new[] { "expense_id" }
                        })
                    })
                }
            };

            // Execute chat completion with function calling loop
            var response = await _client.GetChatCompletionsAsync(options);
            var chatChoice = response.Value.Choices[0];

            // Handle function calls
            while (chatChoice.FinishReason == CompletionsFinishReason.ToolCalls)
            {
                var assistantMessage = new ChatRequestAssistantMessage(chatChoice.Message);
                chatMessages.Add(assistantMessage);

                foreach (var toolCall in chatChoice.Message.ToolCalls.OfType<ChatCompletionsFunctionToolCall>())
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.Name, toolCall.Arguments);
                    chatMessages.Add(new ChatRequestToolMessage(functionResult, toolCall.Id));
                }

                options = new ChatCompletionsOptions(deploymentName, chatMessages);
                response = await _client.GetChatCompletionsAsync(options);
                chatChoice = response.Value.Choices[0];
            }

            return new ChatResponse
            {
                Message = chatChoice.Message.Content,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            return new ChatResponse
            {
                Message = $"Error: {ex.Message}",
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName} with args: {Arguments}", functionName, arguments);

            switch (functionName)
            {
                case "get_expenses":
                    var expenseArgs = JsonSerializer.Deserialize<GetExpensesArgs>(arguments);
                    var expenses = await _expenseService.GetExpensesAsync(searchTerm: expenseArgs?.SearchTerm);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.AmountGBP:F2}",
                        e.StatusName,
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "get_pending_expenses":
                    var pending = await _expenseService.GetPendingExpensesAsync();
                    return JsonSerializer.Serialize(pending.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.AmountGBP:F2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "get_dashboard_stats":
                    var stats = await _expenseService.GetDashboardStatsAsync();
                    return JsonSerializer.Serialize(new
                    {
                        stats.TotalExpenses,
                        stats.PendingApprovals,
                        ApprovedAmount = $"£{stats.ApprovedAmountGBP:F2}",
                        stats.ApprovedCount
                    });

                case "create_expense":
                    var createArgs = JsonSerializer.Deserialize<CreateExpenseArgs>(arguments);
                    if (createArgs == null) return "Error: Invalid arguments";
                    
                    var categories = await _expenseService.GetCategoriesAsync();
                    var category = categories.FirstOrDefault(c => 
                        c.CategoryName.Equals(createArgs.Category, StringComparison.OrdinalIgnoreCase));
                    
                    if (category == null) return $"Error: Category '{createArgs.Category}' not found";
                    
                    var newExpense = await _expenseService.CreateExpenseAsync(new CreateExpenseRequest
                    {
                        UserId = 1, // Default to first user
                        CategoryId = category.CategoryId,
                        Amount = createArgs.Amount,
                        ExpenseDate = DateTime.Parse(createArgs.Date),
                        Description = createArgs.Description
                    });
                    return newExpense > 0 
                        ? $"Created expense with ID {newExpense}" 
                        : "Failed to create expense";

                case "approve_expense":
                    var approveArgs = JsonSerializer.Deserialize<ExpenseIdArgs>(arguments);
                    if (approveArgs == null) return "Error: Invalid arguments";
                    var approved = await _expenseService.ApproveExpenseAsync(approveArgs.ExpenseId, 2); // Manager ID
                    return approved ? "Expense approved successfully" : "Failed to approve expense";

                case "reject_expense":
                    var rejectArgs = JsonSerializer.Deserialize<ExpenseIdArgs>(arguments);
                    if (rejectArgs == null) return "Error: Invalid arguments";
                    var rejected = await _expenseService.RejectExpenseAsync(rejectArgs.ExpenseId, 2);
                    return rejected ? "Expense rejected successfully" : "Failed to reject expense";

                default:
                    return $"Unknown function: {functionName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"Error: {ex.Message}";
        }
    }

    private static string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You can help users with:

1. **View Expenses**: List expenses with optional filters
2. **Dashboard Stats**: Show summary statistics
3. **Pending Approvals**: View expenses awaiting approval
4. **Create Expenses**: Submit new expense claims
5. **Approve/Reject**: Process pending expense approvals (for managers)

When listing expenses, format them nicely with bullet points or numbered lists.
Always be helpful and provide clear, concise responses.
If you execute actions, confirm what was done.

Available categories: Travel, Meals, Supplies, Accommodation, Other
Available statuses: Draft, Submitted, Approved, Rejected";
    }

    private static ChatResponse GetDemoResponse(string message)
    {
        var lowerMessage = message.ToLower();
        string response;

        if (lowerMessage.Contains("expense") && (lowerMessage.Contains("list") || lowerMessage.Contains("show") || lowerMessage.Contains("all")))
        {
            response = @"**Sample Expenses** (Demo Mode - GenAI not deployed)

1. **Travel** - £25.40 - Submitted - Taxi from airport to client site
2. **Meals** - £14.25 - Approved - Client lunch meeting
3. **Supplies** - £7.99 - Draft - Office stationery
4. **Accommodation** - £123.00 - Approved - Hotel during client visit

*To enable full AI capabilities, deploy with: ./deploy-with-chat.sh*";
        }
        else if (lowerMessage.Contains("pending") || lowerMessage.Contains("approval"))
        {
            response = @"**Pending Approvals** (Demo Mode)

1. **ID #1** - Alice Example - Travel - £25.40 - Taxi from airport

*To enable full AI capabilities, deploy with: ./deploy-with-chat.sh*";
        }
        else if (lowerMessage.Contains("dashboard") || lowerMessage.Contains("stats") || lowerMessage.Contains("summary"))
        {
            response = @"**Dashboard Statistics** (Demo Mode)

- **Total Expenses**: 4
- **Pending Approvals**: 1
- **Approved Amount**: £137.25
- **Approved Count**: 2

*To enable full AI capabilities, deploy with: ./deploy-with-chat.sh*";
        }
        else
        {
            response = @"Hello! I'm the Expense Management Assistant (Demo Mode).

I can help you with:
- 📋 View expenses
- 📊 Dashboard statistics
- ⏳ Pending approvals
- ➕ Create new expenses
- ✅ Approve/Reject expenses

**Note**: GenAI services are not deployed. For full AI capabilities including natural language processing, run: `./deploy-with-chat.sh`

Try asking: ""Show me all expenses"" or ""What's pending approval?""";
        }

        return new ChatResponse { Message = response, Success = true };
    }

    // Helper classes for JSON deserialization
    private class GetExpensesArgs
    {
        public string? SearchTerm { get; set; }
        public string? Status { get; set; }
    }

    private class CreateExpenseArgs
    {
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private class ExpenseIdArgs
    {
        public int ExpenseId { get; set; }
    }
}
