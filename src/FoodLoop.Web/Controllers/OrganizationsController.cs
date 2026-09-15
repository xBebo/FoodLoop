using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;

namespace FoodLoop.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class OrganizationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrganizationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض المؤسسات المعلقة فقط (Pending)
        [HttpGet]
        public async Task<IActionResult> PendingRequests()
        {
            var pendingOrgs = await _context.Organizations
                .Where(o => o.Status == OrganizationStatus.Pending)
                .ToListAsync();

            return View(pendingOrgs);
        }

        // 1. القبول (Approve)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOrganization(Guid id)
        {
            var org = await _context.Organizations.FindAsync(id);

            if (org == null)
            {
                TempData["ErrorMessage"] = "المؤسسة غير موجودة.";
                return RedirectToAction(nameof(PendingRequests));
            }

            if (org.Status != OrganizationStatus.Pending)
            {
                TempData["ErrorMessage"] = "تمت معالجة طلب هذه المؤسسة سابقاً.";
                return RedirectToAction(nameof(PendingRequests));
            }

            // استخدام حالة Active المعتمدة في الـ Enum
            org.Status = OrganizationStatus.Active;

            var auditLog = new AuditLog
            {
                EntityType = nameof(Organization),
                EntityId = org.Id,
                Action = "Approve",
                Details = $"Organization '{org.Name}' approved by Admin."
            };
            _context.Set<AuditLog>().Add(auditLog);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم قبول المؤسسة {org.Name} بنجاح.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "حدث تعارض أثناء الحفظ، يرجى إعادة المحاولة.";
            }

            return RedirectToAction(nameof(PendingRequests));
        }

        // 2. الرفض (Reject)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectOrganization(Guid id)
        {
            var org = await _context.Organizations.FindAsync(id);

            if (org == null)
            {
                TempData["ErrorMessage"] = "المؤسسة غير موجودة.";
                return RedirectToAction(nameof(PendingRequests));
            }

            if (org.Status != OrganizationStatus.Pending)
            {
                TempData["ErrorMessage"] = "تمت معالجة طلب هذه المؤسسة سابقاً.";
                return RedirectToAction(nameof(PendingRequests));
            }

            org.Status = OrganizationStatus.Rejected;

            var auditLog = new AuditLog
            {
                EntityType = nameof(Organization),
                EntityId = org.Id,
                Action = "Reject",
                Details = $"Organization '{org.Name}' rejected by Admin."
            };
            _context.Set<AuditLog>().Add(auditLog);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم رفض المؤسسة {org.Name}.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "حدث تعارض أثناء الحفظ، يرجى إعادة المحاولة.";
            }

            return RedirectToAction(nameof(PendingRequests));
        }
    }
}