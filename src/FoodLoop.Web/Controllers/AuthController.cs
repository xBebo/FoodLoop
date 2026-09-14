using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // 1. عرض شاشة تسجيل مؤسسة جديدة
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // 2. منطق تسجيل المؤسسة والمستخدم التابع لها
        [HttpPost]
        public async Task<IActionResult> Register(string orgName, string licenseNumber, string email, string password)
        {
            var org = new Organization
            {
                Name = orgName,
                LicenseNumber = licenseNumber,
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
                TempData["SuccessMessage"] = "تم تقديم طلب التسجيل بنجاح! في انتظار موافقة الأدمن لتفعيل الحساب.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // 3. عرض شاشة تسجيل الدخول
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 4. منطق تسجيل الدخول (المعدّل بناءً على ملاحظة زملائك)
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "بيانات الدخول غير صحيحة.");
                return View();
            }

            // يتم الفحص فقط إذا كان المستخدم ينتمي لمؤسسة (أي ليس Admin أو Courier) وكانت المؤسسة ليست Active
            if (user.Organization != null && user.Organization.Status != OrganizationStatus.Active)
            {
                ModelState.AddModelError("", "حساب المؤسسة الخاص بك في انتظار موافقة الأدمن.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, password, false, false);
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "كلمة المرور غير صحيحة.");
            return View();
        }

        // 5. تسجيل الخروج
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}