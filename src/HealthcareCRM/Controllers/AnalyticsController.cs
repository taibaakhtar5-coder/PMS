using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;

namespace HealthcareCRM.Controllers
{
    [JwtAuthorize]
    public class AnalyticsController : Controller
    {
        public IActionResult Index() => View();
    }
}
