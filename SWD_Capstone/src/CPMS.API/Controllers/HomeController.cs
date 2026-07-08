using Microsoft.AspNetCore.Mvc;
using CPMS.Api.Models;

namespace CPMS.Api.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        var model = new DashboardViewModel(
            "CPMS - Capstone Project Management System",
            "PostgreSQL",
            "CPMS.API + CPMS.Application + CPMS.Domain + CPMS.Infrastructure");

        return View(model);
    }
}
