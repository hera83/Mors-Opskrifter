using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Models;

namespace web.Controllers
{
    public class SetupController : Controller
    {
        private readonly UserManager<ApplicationUser>  _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly RoleManager<IdentityRole> _roles;

        public SetupController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, RoleManager<IdentityRole> roles)
        {
            _users  = users;
            _signIn = signIn;
            _roles  = roles;
        }

        private async Task<bool> HasUsersAsync() =>
            await _users.Users.AnyAsync();

        // GET /Setup
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (await HasUsersAsync())
                return RedirectToAction("Login", "Auth");

            return View();
        }

        // POST /Setup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SetupViewModel model)
        {
            if (await HasUsersAsync())
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
                return View(model);

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Adgangskoderne er ikke ens.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName    = model.Username,
                DisplayName = model.DisplayName,
                Email       = model.Email,
            };

            var result = await _users.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return View(model);
            }

            await _users.AddToRoleAsync(user, "Administrator");
            await _signIn.SignInAsync(user, isPersistent: true);
            return RedirectToAction("Index", "Recipes");
        }
    }

    public class SetupViewModel
    {
        public string Username        { get; set; } = "";
        public string DisplayName     { get; set; } = "";
        public string Email           { get; set; } = "";
        public string Password        { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }
}
