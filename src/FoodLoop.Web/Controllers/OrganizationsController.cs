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

        // 1. عرض المؤسسات المنتظرة للقبول
        public async Task<IActionResult> PendingRequests()
        {
            var pendingOrgs = await _context.Organizations
                .Where(o => o.Status == OrganizationStatus.Pending)
                .ToListAsync();

            return View(pendingOrgs);
        }

        // 2. قبول المؤسسة
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var org = await _context.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            org.Status = OrganizationStatus.Active;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(PendingRequests));
        }

        // 3. رفض المؤسسة
        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var org = await _context.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            org.Status = OrganizationStatus.Rejected;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(PendingRequests));
        }
    }
}