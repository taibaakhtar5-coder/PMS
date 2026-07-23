using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;

namespace HealthcareCRM.Controllers
{
    [JwtAuthorize]
    public class NotificationController : Controller
    {
        public IActionResult List() => View();
    }
}
