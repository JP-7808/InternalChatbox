using InternalChatbox.Data;
using InternalChatbox.Helpers;
using InternalChatbox.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace InternalChatbox.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(User model, string password)
        {
            ModelState.Remove("PasswordHash"); // Prevent model validation error for null PasswordHash

            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("", "Email already registered.");
                    return View(model);
                }

                var user = new User
                {
                    Name = model.Name,
                    Designation = model.Designation,
                    Location = model.Location,
                    Email = model.Email,
                    PasswordHash = PasswordHelper.HashPassword(password),
                    Role = "Employee",
                    Status = "Offline"
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login");
            }

            return View(model);
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var hashedPassword = PasswordHelper.HashPassword(password);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashedPassword);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserName", user.Name);
                return RedirectToAction("Index", "Chat");
            }

            ViewBag.Error = "Invalid email or password.";
            return View();
        }

        // Add these methods to your AccountController

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(User updatedUser, string currentPassword, string newPassword)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            // Update basic info
            user.Name = updatedUser.Name;
            user.Designation = updatedUser.Designation;
            user.Location = updatedUser.Location;

            // Update password if provided
            if (!string.IsNullOrEmpty(currentPassword) && !string.IsNullOrEmpty(newPassword))
            {
                if (PasswordHelper.HashPassword(currentPassword) != user.PasswordHash)
                {
                    ModelState.AddModelError("", "Current password is incorrect");
                    return View("Profile", user);
                }

                user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            }

            await _context.SaveChangesAsync();

            // Update session name if changed
            HttpContext.Session.SetString("UserName", user.Name);

            ViewBag.SuccessMessage = "Profile updated successfully!";
            return View("Profile", user);
        }

        // GET: /Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
