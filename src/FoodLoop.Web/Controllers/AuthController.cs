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
        public async Task<IActionResult> Register(string orgName, string licenseNumber, OrganizationType orgType, string email, string password)
        {
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
                if (await _roleManager.RoleExistsAsync(roleName))
                {
                    await _userManager.AddToRoleAsync(user, roleName);
                }

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
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError("", "بيانات الدخول غير صحيحة.");
                return View();
            }

            if (user.Organization != null)
            {
                if (user.Organization.Status == OrganizationStatus.Pending)
                {
                    ModelState.AddModelError("", "حساب المؤسسة الخاص بك ما زال في انتظار موافقة الأدمن.");
                    return View();
                }
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, password, false, false);
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "كلمة المرور غير صحيحة.");
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ApproveOrganization(Guid id)
        {
            var org = await _context.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            org.Status = OrganizationStatus.Active;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تمت الموافقة على المؤسسة بنجاح.";
            return RedirectToAction("PendingRequests", "Organizations");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> RejectOrganization(Guid id)
        {
            var org = await _context.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            org.Status = OrganizationStatus.Rejected;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم رفض طلب المؤسسة.";
            return RedirectToAction("PendingRequests", "Organizations");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}