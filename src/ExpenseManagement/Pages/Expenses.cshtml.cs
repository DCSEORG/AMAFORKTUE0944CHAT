using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    public ExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Expenses = await _expenseService.GetExpensesAsync(statusId: StatusId, categoryId: CategoryId, searchTerm: SearchTerm);
        Statuses = await _expenseService.GetStatusesAsync();
        Categories = await _expenseService.GetCategoriesAsync();
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        await _expenseService.SubmitExpenseAsync(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _expenseService.DeleteExpenseAsync(id);
        return RedirectToPage();
    }
}
