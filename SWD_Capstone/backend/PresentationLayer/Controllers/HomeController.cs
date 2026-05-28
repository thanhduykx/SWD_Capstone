using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Models;

namespace PresentationLayer.Controllers;

public sealed class HomeController(IConfiguration configuration) : Controller
{
    public IActionResult Index()
    {
        var model = new DashboardViewModel(
            "CPMS - Capstone Project Management System",
            "PostgreSQL",
            configuration["DefaultAccount:Username"] ?? "admin",
            "MVC PresentationLayer + ServiceLayer + DataAccessLayer");

        return View(model);
    }
}
