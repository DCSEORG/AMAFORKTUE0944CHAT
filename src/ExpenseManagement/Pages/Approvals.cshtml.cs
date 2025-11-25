using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
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
        // Default to manager user ID 2 for demo
        await _expenseService.ApproveExpenseAsync(id, 2);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        // Default to manager user ID 2 for demo
        await _expenseService.RejectExpenseAsync(id, 2);
        return RedirectToPage();
    }
}
