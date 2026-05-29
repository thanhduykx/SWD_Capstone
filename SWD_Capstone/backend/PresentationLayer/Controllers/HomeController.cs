using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Models;

namespace PresentationLayer.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        var model = new DashboardViewModel(
            "CPMS - Capstone Project Management System",
            "PostgreSQL",
            "MVC PresentationLayer + ServiceLayer + DataAccessLayer");

        return View(model);
    }
}
