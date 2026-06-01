using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Models;

namespace web.Controllers
{
    public class AuthController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly UserManager<ApplicationUser>  _users;

        public AuthController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
        {
            _signIn = signIn;
            _users  = users;
        }

        // GET /Auth/Login
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Recipes");

            // Ingen brugere endnu → send til setup
            if (!await _users.Users.AnyAsync())
                return RedirectToAction("Index", "Setup");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var result = await _signIn.PasswordSignInAsync(
                model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Recipes");
            }

            ModelState.AddModelError(string.Empty, "Forkert brugernavn eller adgangskode.");
            return View(model);
        }

        // POST /Auth/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Login");
        }
    }

    public class LoginViewModel
    {
        public string Username   { get; set; } = "";
        public string Password   { get; set; } = "";
        public bool   RememberMe { get; set; }
    }
}
