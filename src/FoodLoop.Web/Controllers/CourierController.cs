using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;

namespace FoodLoop.Web.Controllers
{
    [Authorize]
    public class CourierController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CourierController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. عرض المهام الموكلة للمندوب الحالي (My Tasks)
        public async Task<IActionResult> MyTasks()
        {
            var tasks = await _context.FoodDonations
                .Where(d => d.Status == DonationStatus.Claimed || d.Status == DonationStatus.InTransit)
                .ToListAsync();

            return View(tasks);
        }

        // 2. شاشة للأدمن لاختيار وتعيين مندوب لمهمة تبرع (Assign Courier)
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AssignCourier()
        {
            var claimedDonations = await _context.FoodDonations
                .Where(d => d.Status == DonationStatus.Claimed)
                .ToListAsync();

            return View(claimedDonations);
        }

        // 3. تأكيد تعيين المندوب للمهمة
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AssignCourier(int donationId, string courierId)
        {
            var donation = await _context.FoodDonations.FindAsync(donationId);
            if (donation == null) return NotFound();

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعيين المندوب بنجاح للمهمة.";
            return RedirectToAction(nameof(AssignCourier));
        }

        // 4. شاشة إدخال كود الاستلام / التسليم للتحقق (Handover)
        [HttpGet]
        public IActionResult VerifyHandover(int donationId, string type)
        {
            ViewBag.DonationId = donationId;
            ViewBag.Type = type;
            return View();
        }

        // 5. منطق التحقق من الكود وتغيير حالة التبرع
        [HttpPost]
        public async Task<IActionResult> VerifyHandover(int donationId, string code, string type)
        {
            var donation = await _context.FoodDonations.FindAsync(donationId);
            if (donation == null) return NotFound();

            if (type == "Pickup")
            {
                donation.Status = DonationStatus.InTransit;
                TempData["SuccessMessage"] = "تم استلام التبرع بنجاح، والحالة الآن: قيد التوصيل.";
            }
            else if (type == "Delivery")
            {
                donation.Status = DonationStatus.Delivered;
                TempData["SuccessMessage"] = "تم تسليم التبرع للمستفيد بنجاح وإغلاق المهمة.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(MyTasks));
        }
    }
}