using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null, string? searchTerm = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<List<Expense>> GetPendingExpensesAsync(string? searchTerm = null);
    Task<List<ExpenseCategory>> GetCategoriesAsync();
    Task<List<ExpenseStatus>> GetStatusesAsync();
    Task<List<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(int userId);
    Task<List<User>> GetManagersAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<List<Expense>> GetRecentExpensesAsync(int topCount = 10);
    
    bool UseDummyData { get; }
    string? LastError { get; }
    string? LastErrorFile { get; }
    int? LastErrorLine { get; }
}

public class ExpenseService : IExpenseService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<ExpenseService> _logger;

    public bool UseDummyData { get; private set; }
    public string? LastError { get; private set; }
    public string? LastErrorFile { get; private set; }
    public int? LastErrorLine { get; private set; }

    public ExpenseService(IDatabaseService databaseService, ILogger<ExpenseService> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    private void SetError(Exception ex, string file, int line)
    {
        UseDummyData = true;
        LastError = _databaseService.LastError ?? ex.Message;
        LastErrorFile = file;
        LastErrorLine = line;
        _logger.LogError(ex, "Database error in {File}:{Line}: {Message}", file, line, ex.Message);
    }

    public async Task<List<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null, string? searchTerm = null)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
            command.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
            command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            
            UseDummyData = false;
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 73);
            return GetDummyExpenses();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetExpenseById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                UseDummyData = false;
                return MapExpense(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 96);
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_CreateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            UseDummyData = false;
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 121);
            return -1;
        }
    }

    public async Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_UpdateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            UseDummyData = false;
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 145);
            return false;
        }
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_DeleteExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            UseDummyData = false;
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 164);
            return false;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_SubmitExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            UseDummyData = false;
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 183);
            return false;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_ApproveExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            UseDummyData = false;
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 203);
            return false;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_RejectExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            UseDummyData = false;
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 223);
            return false;
        }
    }

    public async Task<List<Expense>> GetPendingExpensesAsync(string? searchTerm = null)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetPendingExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            
            UseDummyData = false;
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 248);
            return GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
        }
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetCategories", connection);
            command.CommandType = CommandType.StoredProcedure;

            var categories = new List<ExpenseCategory>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            
            UseDummyData = false;
            return categories;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 279);
            return GetDummyCategories();
        }
    }

    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetStatuses", connection);
            command.CommandType = CommandType.StoredProcedure;

            var statuses = new List<ExpenseStatus>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
            
            UseDummyData = false;
            return statuses;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 308);
            return GetDummyStatuses();
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetUsers", connection);
            command.CommandType = CommandType.StoredProcedure;

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }
            
            UseDummyData = false;
            return users;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 333);
            return GetDummyUsers();
        }
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetUserById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                UseDummyData = false;
                return MapUser(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 356);
            return GetDummyUsers().FirstOrDefault(u => u.UserId == userId);
        }
    }

    public async Task<List<User>> GetManagersAsync()
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetManagers", connection);
            command.CommandType = CommandType.StoredProcedure;

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            
            UseDummyData = false;
            return users;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 389);
            return GetDummyUsers().Where(u => u.RoleName == "Manager").ToList();
        }
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetDashboardStats", connection);
            command.CommandType = CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                UseDummyData = false;
                return new DashboardStats
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    ApprovedAmountMinor = reader.GetInt32(reader.GetOrdinal("ApprovedAmountMinor")),
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                };
            }
            return new DashboardStats();
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 417);
            return GetDummyStats();
        }
    }

    public async Task<List<Expense>> GetRecentExpensesAsync(int topCount = 10)
    {
        try
        {
            using var connection = await _databaseService.GetConnectionAsync();
            using var command = new SqlCommand("dbo.usp_GetRecentExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TopCount", topCount);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            
            UseDummyData = false;
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 442);
            return GetDummyExpenses().Take(topCount).ToList();
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            AmountGBP = reader.GetDecimal(reader.GetOrdinal("AmountGBP")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
            ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
            ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // Dummy data methods for fallback when database is unavailable
    private static List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, AmountGBP = 25.40m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-5), Description = "Taxi from airport to client site", CreatedAt = DateTime.Now.AddDays(-5) },
            new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, AmountGBP = 14.25m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-30), Description = "Client lunch meeting", CreatedAt = DateTime.Now.AddDays(-30) },
            new() { ExpenseId = 3, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, AmountGBP = 7.99m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-1), Description = "Office stationery", CreatedAt = DateTime.Now.AddDays(-1) },
            new() { ExpenseId = 4, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, AmountGBP = 123.00m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-60), Description = "Hotel during client visit", CreatedAt = DateTime.Now.AddDays(-60) }
        };
    }

    private static List<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private static List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", ManagerId = 2, ManagerName = "Bob Manager", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-6) },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-12) }
        };
    }

    private static DashboardStats GetDummyStats()
    {
        return new DashboardStats
        {
            TotalExpenses = 4,
            PendingApprovals = 1,
            ApprovedAmountMinor = 13725,
            ApprovedCount = 2
        };
    }
}
