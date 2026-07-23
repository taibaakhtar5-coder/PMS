using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;

namespace HealthcareCRM.Controllers
{
    [JwtAuthorize]
    public class PrescriptionController : Controller
    {
        public IActionResult List() => View();
        public IActionResult Detail(Guid id) { ViewBag.PrescriptionId = id; return View(); }
    }
}
