using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        // 1. شاشة تسجيل الدخول
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        // 2. منطق تسجيل الدخول
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "بيانات الدخول غير صحيحة.");
                return View();
            }
            // التأكد من أن المؤسسة التابع لها المستخدم مقبولة من الأدمن
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
        // 3. تسجيل الخروج
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}