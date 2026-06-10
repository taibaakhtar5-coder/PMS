using System;
using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;

namespace HealthcareCRM.Controllers
{
    [JwtAuthorize]
    public class PatientController : Controller
    {
        /// <summary>
        /// GET /Patient/List
        /// Renders the patient registry list page.
        /// </summary>
        [HttpGet]
        public IActionResult List()
        {
            return View();
        }

        /// <summary>
        /// GET /Patient/Add
        /// Renders the patient registration form.
        /// </summary>
        [HttpGet]
        public IActionResult Add()
        {
            return View();
        }

        /// <summary>
        /// GET /Patient/Edit/{id}
        /// Renders the patient edit form for the specified ID.
        /// </summary>
        [HttpGet]
        public IActionResult Edit(Guid id)
        {
            ViewBag.PatientId = id;
            return View();
        }
    }
}
