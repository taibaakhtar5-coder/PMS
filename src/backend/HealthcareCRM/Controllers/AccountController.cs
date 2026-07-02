using Microsoft.AspNetCore.Mvc;

namespace HealthcareCRM.Controllers
{
    public class AccountController : Controller
    {
        /// <summary>
        /// GET /Account/Login
        /// Renders the login screen.
        /// </summary>
        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in (cookie exists), redirect to Dashboard (Home/Index)
            if (Request.Cookies.ContainsKey("jwt_token"))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        /// <summary>
        /// GET /Account/Register
        /// Renders the registration screen.
        /// </summary>
        [HttpGet]
        public IActionResult Register()
        {
            if (Request.Cookies.ContainsKey("jwt_token"))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        /// <summary>
        /// GET /Account/Logout
        /// Clears the authentication cookie and redirects to the Login page.
        /// </summary>
        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt_token");
            return RedirectToAction("Login");
        }
    }
}
