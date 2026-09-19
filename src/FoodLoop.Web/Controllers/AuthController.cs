using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;

namespace FoodLoop.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ApplicationDbContext _context;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string orgName, string licenseNumber, OrganizationType orgType, string email, string password)
        {
            if (!ModelState.IsValid || !Enum.IsDefined(typeof(OrganizationType), orgType))
            {
                ModelState.AddModelError("", "نوع المؤسسة غير صحيح.");
                return View();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var org = new Organization
            {
                Name = orgName,
                LicenseNumber = licenseNumber,
                Type = orgType,
                Status = OrganizationStatus.Pending
            };

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                Organization = org
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                string roleName = orgType == OrganizationType.Donor ? "Donor" : "Beneficiary";

                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    ModelState.AddModelError("", "Account roles are not configured. Contact the administrator.");
                    return View();
                }

                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!roleResult.Succeeded)
                {
                    ModelState.AddModelError("", "حدث خطأ أثناء تعيين الصلاحية للمستخدم.");
                    return View();
                }

                await transaction.CommitAsync();
                TempData["SuccessMessage"] = "تم تقديم طلب التسجيل بنجاح! في انتظار موافقة الأدمن لتفعيل الحساب.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            var user = await _context.Users
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError("", "بيانات الدخول غير صحيحة.");
                return View();
            }

            if (user.Organization != null && (user.Organization.Status is OrganizationStatus.Pending or OrganizationStatus.Rejected ||
                (user.Organization.Status == OrganizationStatus.Suspended && user.Organization.Type != OrganizationType.Beneficiary)))
            {
                ModelState.AddModelError("", "حساب المؤسسة الخاص بك ما زال في انتظار موافقة الأدمن.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, password, false, false);
            if (result.Succeeded)
            {
                // Safe ReturnUrl Check: Only local URLs, strictly ignoring external / scheme-relative URLs
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "كلمة المرور غير صحيحة.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}