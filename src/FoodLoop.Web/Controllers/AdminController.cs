using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. عرض قائمة المؤسسات مع الفلترة والـ Pagination (20 عنصر في الصفحة)
        public async Task<IActionResult> Organizations(OrganizationStatus? status, int page = 1)
        {
            int pageSize = 20;
            if (page < 1) page = 1;

            var query = _context.Organizations.AsNoTracking().AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            int totalItems = await query.CountAsync();
            var organizations = await query
                .OrderBy(o => o.Name)
                .ThenBy(o => o.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(organizations);
        }

        // 2. تعليق حساب المؤسسة (Suspend)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendOrganization(Guid id)
        {
            var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id);
            if (org == null) return NotFound();

            if (org.Status != OrganizationStatus.Active)
            {
                TempData["ErrorMessage"] = "Only active organizations can be suspended.";
                return RedirectToAction(nameof(Organizations));
            }

            org.Status = OrganizationStatus.Suspended;

            // إضافة الـ Audit Log في نفس الـ DbContext
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(currentUserIdStr, out Guid currentUserId);

            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "OrganizationSuspended",
                EntityType = "Organization",
                EntityId = org.Id,
                ActorUserId = currentUserId != Guid.Empty ? currentUserId : null,
                CreatedAtUtc = DateTime.UtcNow,
                Details = $"Organization '{org.Name}' was suspended by Admin."
            };
            _context.AuditLogs.Add(audit);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Organization '{org.Name}' has been suspended.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "A concurrency conflict occurred. The organization status might have changed.";
            }

            return RedirectToAction(nameof(Organizations));
        }

        // 3. إعادة تفعيل المؤسسة (Reactivate)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateOrganization(Guid id)
        {
            var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id);
            if (org == null) return NotFound();

            if (org.Status != OrganizationStatus.Suspended)
            {
                TempData["ErrorMessage"] = "Only suspended organizations can be reactivated.";
                return RedirectToAction(nameof(Organizations));
            }

            org.Status = OrganizationStatus.Active;

            // إضافة الـ Audit Log في نفس الـ DbContext
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(currentUserIdStr, out Guid currentUserId);

            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "OrganizationReactivated",
                EntityType = "Organization",
                EntityId = org.Id,
                ActorUserId = currentUserId != Guid.Empty ? currentUserId : null,
                CreatedAtUtc = DateTime.UtcNow,
                Details = $"Organization '{org.Name}' was reactivated by Admin."
            };
            _context.AuditLogs.Add(audit);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Organization '{org.Name}' has been reactivated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "A concurrency conflict occurred. The organization status might have changed.";
            }

            return RedirectToAction(nameof(Organizations));
        }

        // 4. الإبقاء على صفحة الطلبات المعلقة PendingRequests
        public async Task<IActionResult> PendingRequests()
        {
            var pendingOrgs = await _context.Organizations
                .AsNoTracking()
                .Where(o => o.Status == OrganizationStatus.Pending)
                .OrderBy(o => o.Name)
                .ToListAsync();

            return View(pendingOrgs);
        }
    }
}