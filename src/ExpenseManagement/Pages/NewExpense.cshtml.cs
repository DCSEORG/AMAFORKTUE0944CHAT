using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    // Demo user ID - in production, this would come from authentication context
    private const int DefaultEmployeeUserId = 1;
    
    private readonly IExpenseService _expenseService;

    public List<ExpenseCategory> Categories { get; set; } = new();

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    public int CategoryId { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public NewExpenseModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
        if (Categories.Any())
        {
            CategoryId = Categories.First().CategoryId;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();

        if (Amount <= 0)
        {
            ErrorMessage = "Amount must be greater than zero.";
            return Page();
        }

        var request = new CreateExpenseRequest
        {
            UserId = DefaultEmployeeUserId,
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        var expenseId = await _expenseService.CreateExpenseAsync(request);

        if (expenseId > 0)
        {
            return RedirectToPage("/Expenses");
        }

        ErrorMessage = "Failed to create expense. Please try again.";
        return Page();
    }
}
