using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web.Models;

namespace web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;

        public UsersController(UserManager<ApplicationUser> users)
        {
            _users = users;
        }

        // POST /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string username, string displayName, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Json(new { ok = false, error = "Brugernavn og adgangskode er påkrævet." });

            var user = new ApplicationUser
            {
                UserName    = username.Trim(),
                DisplayName = displayName?.Trim() ?? username.Trim(),
            };

            var result = await _users.CreateAsync(user, password);
            if (!result.Succeeded)
                return Json(new { ok = false, error = string.Join(" ", result.Errors.Select(e => e.Description)) });

            var assignedRole = role == "Administrator" ? "Administrator" : "User";
            await _users.AddToRoleAsync(user, assignedRole);

            return Json(new { ok = true, id = user.Id, username = user.UserName, displayName = user.DisplayName, role = assignedRole });
        }

        // POST /Users/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string displayName, string? newPassword, string role)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return Json(new { ok = false, error = "Bruger ikke fundet." });

            user.DisplayName = displayName?.Trim() ?? user.DisplayName;
            var updateResult = await _users.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return Json(new { ok = false, error = string.Join(" ", updateResult.Errors.Select(e => e.Description)) });

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _users.ResetPasswordAsync(user, token, newPassword);
                if (!pwResult.Succeeded)
                    return Json(new { ok = false, error = string.Join(" ", pwResult.Errors.Select(e => e.Description)) });
            }

            // Opdater rolle
            var currentRoles = await _users.GetRolesAsync(user);
            await _users.RemoveFromRolesAsync(user, currentRoles);
            var assignedRole = role == "Administrator" ? "Administrator" : "User";
            await _users.AddToRoleAsync(user, assignedRole);

            return Json(new { ok = true, displayName = user.DisplayName, role = assignedRole });
        }

        // POST /Users/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return Json(new { ok = false, error = "Bruger ikke fundet." });

            // Forhindre sletning af den eneste administrator
            var admins = await _users.GetUsersInRoleAsync("Administrator");
            var userRoles = await _users.GetRolesAsync(user);
            if (userRoles.Contains("Administrator") && admins.Count <= 1)
                return Json(new { ok = false, error = "Der skal altid være mindst én administrator." });

            await _users.DeleteAsync(user);
            return Json(new { ok = true });
        }

        // GET /Users/List  – returnerer alle brugere som JSON til settings-siden
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var users = _users.Users.ToList();
            var result = new List<object>();
            foreach (var u in users)
            {
                var roles = await _users.GetRolesAsync(u);
                result.Add(new
                {
                    id          = u.Id,
                    username    = u.UserName,
                    displayName = u.DisplayName,
                    role        = roles.FirstOrDefault() ?? "User"
                });
            }
            return Json(result);
        }
    }
}
