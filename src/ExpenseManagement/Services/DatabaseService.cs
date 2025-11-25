using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpenseManagement.Services;

public interface IDatabaseService
{
    Task<SqlConnection> GetConnectionAsync();
    bool IsConnected { get; }
    string? LastError { get; }
    string? LastErrorFile { get; }
    int? LastErrorLine { get; }
}

public class DatabaseService : IDatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseService> _logger;

    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    public string? LastErrorFile { get; private set; }
    public int? LastErrorLine { get; private set; }

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SqlConnection> GetConnectionAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
            }

            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            IsConnected = true;
            LastError = null;
            LastErrorFile = null;
            LastErrorLine = null;
            
            return connection;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = GetDetailedErrorMessage(ex);
            LastErrorFile = "Services/DatabaseService.cs";
            LastErrorLine = 45;
            
            _logger.LogError(ex, "Failed to connect to database: {Message}", ex.Message);
            throw;
        }
    }

    private string GetDetailedErrorMessage(Exception ex)
    {
        var message = ex.Message;
        
        if (ex.Message.Contains("Managed Identity") || ex.Message.Contains("DefaultAzureCredential"))
        {
            message += "\n\nManaged Identity Fix:\n" +
                "1. Ensure AZURE_CLIENT_ID is set in App Service configuration\n" +
                "2. Verify the managed identity has db_datareader and db_datawriter roles\n" +
                "3. Run: python3 run-sql-dbrole.py to configure database permissions\n" +
                "4. Check the connection string uses 'Authentication=Active Directory Managed Identity'";
        }
        else if (ex.Message.Contains("Login failed") || ex.Message.Contains("Cannot open database"))
        {
            message += "\n\nDatabase Access Fix:\n" +
                "1. Verify the database exists and is accessible\n" +
                "2. Check the managed identity is added as a user in the database\n" +
                "3. Ensure firewall rules allow access from App Service";
        }
        
        return message;
    }
}
