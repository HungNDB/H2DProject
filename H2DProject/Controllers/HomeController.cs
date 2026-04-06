using Microsoft.AspNetCore.Mvc;

namespace H2DProject.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => RedirectToAction("Index", "Order");

        public IActionResult Error() => View();
    }
}
