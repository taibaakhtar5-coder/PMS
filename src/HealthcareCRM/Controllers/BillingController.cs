using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;

namespace HealthcareCRM.Controllers
{
    [JwtAuthorize]
    public class BillingController : Controller
    {
        public IActionResult List() => View();
        public IActionResult Detail(Guid id) { ViewBag.InvoiceId = id; return View(); }
    }
}
