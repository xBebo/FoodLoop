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
    [Authorize]
    public class CourierController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CourierController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AssignCourier(Guid claimId)
        {
            var claim = await _context.DonationClaims
                .Include(c => c.FoodDonation)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            var couriers = await _userManager.GetUsersInRoleAsync("Courier");
            ViewBag.Couriers = couriers;

            return View(claim);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AssignCourier(Guid claimId, Guid courierUserId)
        {
            var claim = await _context.DonationClaims
                .Include(c => c.FoodDonation)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            var courierUser = await _userManager.FindByIdAsync(courierUserId.ToString());
            if (courierUser == null || !await _userManager.IsInRoleAsync(courierUser, "Courier"))
            {
                ModelState.AddModelError("", "المستخدم المختار ليس مندوب توصيل معتمد.");
                var couriers = await _userManager.GetUsersInRoleAsync("Courier");
                ViewBag.Couriers = couriers;
                return View(claim);
            }

            claim.AssignedCourierUserId = courierUserId;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعيين المندوب للمهمة بنجاح.";
            return RedirectToAction("MyTasks");
        }

        [Authorize(Roles = "Courier")]
        [HttpGet]
        public async Task<IActionResult> MyTasks()
        {
            var currentUserId = Guid.Parse(_userManager.GetUserId(User)!);

            var myTasks = await _context.DonationClaims
                .Include(c => c.FoodDonation)
                .Where(c => c.AssignedCourierUserId == currentUserId)
                .ToListAsync();

            return View(myTasks);
        }

        [Authorize(Roles = "Courier")]
        [HttpGet]
        public async Task<IActionResult> VerifyHandover(Guid claimId)
        {
            var claim = await _context.DonationClaims
                .Include(c => c.FoodDonation)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            var currentUserId = Guid.Parse(_userManager.GetUserId(User)!);
            if (claim.AssignedCourierUserId != currentUserId)
            {
                return Forbid();
            }

            return View(claim);
        }

        [Authorize(Roles = "Courier")]
        [HttpPost]
        public async Task<IActionResult> VerifyHandover(Guid claimId, string handoverToken, HandoverType handoverType)
        {
            var currentUserId = Guid.Parse(_userManager.GetUserId(User)!);

            var claim = await _context.DonationClaims
                .Include(c => c.FoodDonation)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            if (claim.AssignedCourierUserId != currentUserId)
            {
                ModelState.AddModelError("", "غير مصرح لك بتنفيذ هذه العملية لمهمة غير مسندة إليك.");
                return View(claim);
            }

            if (handoverType == HandoverType.Pickup)
            {
                if (claim.FoodDonation != null) claim.FoodDonation.Status = DonationStatus.InTransit;
            }
            else if (handoverType == HandoverType.Delivery)
            {
                if (claim.FoodDonation != null && claim.FoodDonation.Status != DonationStatus.InTransit)
                {
                    ModelState.AddModelError("", "لا يمكن إتمام عملية التسليم قبل إثبات استلام الشحنة (Pickup) أولاً.");
                    return View(claim);
                }

                // استخدام الحالة المتوفرة في Enum الخاصة بالتبرع
                if (claim.FoodDonation != null) claim.FoodDonation.Status = DonationStatus.InTransit;
            }

            // إنشاء السجل بالخصائص المعتمدة في كلاس HandoverRecord
            var handoverRecord = new HandoverRecord
            {
                DonationClaimId = claimId,
                CourierUserId = currentUserId,
                Type = handoverType
            };                     
            _context.HandoverRecords.Add(handoverRecord);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = handoverType == HandoverType.Pickup ? "تم إثبات استلام الشحنة بنجاح." : "تم إثبات تسليم الشحنة بنجاح.";
            return RedirectToAction("MyTasks");
        }
    }
}