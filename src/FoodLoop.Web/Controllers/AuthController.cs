using Microsoft.AspNetCore.Mvc;
using FoodLoop.Application.Identity;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly AccountService _accounts;

        public AuthController(AccountService accounts)
        {
            _accounts = accounts;
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

            var result = await _accounts.RegisterAsync(new RegisterOrganizationRequest(orgName, licenseNumber, orgType, email), password);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "تم تقديم طلب التسجيل بنجاح! في انتظار موافقة الأدمن لتفعيل الحساب.";
                return RedirectToAction("Login");
            }

            switch (result.Outcome)
            {
                case RegistrationOutcome.IdentityFailed or RegistrationOutcome.DuplicateAccount when result.Errors.Count > 0:
                    foreach (var error in result.Errors) ModelState.AddModelError("", error);
                    break;
                default:
                    ModelState.AddModelError("", RegistrationMessage(result.Outcome));
                    break;
            }

            return View();
        }

        private static string RegistrationMessage(RegistrationOutcome outcome) => outcome switch
        {
            RegistrationOutcome.InvalidOrganizationType => "نوع المؤسسة غير صحيح.",
            RegistrationOutcome.InvalidInput => "Organization name, license number and password are required.",
            RegistrationOutcome.DuplicateLicense => "An organization with this license number is already registered.",
            RegistrationOutcome.DuplicateAccount => "An account with this email is already registered.",
            RegistrationOutcome.RolesNotConfigured => "Account roles are not configured. Contact the administrator.",
            RegistrationOutcome.RoleAssignmentFailed => "حدث خطأ أثناء تعيين الصلاحية للمستخدم.",
            _ => "Registration could not be completed."
        };

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

            switch (await _accounts.SignInAsync(email, password))
            {
                case LoginOutcome.Succeeded:
                    // Safe ReturnUrl Check: Only local URLs, strictly ignoring external / scheme-relative URLs
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                case LoginOutcome.UnknownAccount:
                    ModelState.AddModelError("", "بيانات الدخول غير صحيحة.");
                    break;
                case LoginOutcome.OrganizationNotActive:
                    ModelState.AddModelError("", "حساب المؤسسة الخاص بك ما زال في انتظار موافقة الأدمن.");
                    break;
                default:
                    ModelState.AddModelError("", "كلمة المرور غير صحيحة.");
                    break;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _accounts.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}
