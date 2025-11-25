using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    // Demo user ID - in production, this would come from authentication context
    private const int DefaultManagerUserId = 2;
    
    private readonly IExpenseService _expenseService;

    public List<Expense> PendingExpenses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    public ApprovalsModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        PendingExpenses = await _expenseService.GetPendingExpensesAsync(SearchTerm);
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        await _expenseService.ApproveExpenseAsync(id, DefaultManagerUserId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        await _expenseService.RejectExpenseAsync(id, DefaultManagerUserId);
        return RedirectToPage();
    }
}
