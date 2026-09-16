using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Claims;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Web.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ClaimService _claimService;

        public ClaimsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public ClaimsController(ClaimService claimService)
        {
            _claimService = claimService;
        }

        public ClaimsController(object service)
        {
        }

        // 1. Mine
        public async Task<IActionResult> Mine()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId)) return Unauthorized();

            return await Mine(userId);
        }

        [NonAction]
        public async Task<IActionResult> Mine(Guid userId)
        {
            if (_context != null)
            {
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null || !user.OrganizationId.HasValue) return Forbid();

                var claims = await _context.DonationClaims
                    .AsNoTracking()
                    .Include(c => c.FoodDonation)
                    .Where(c => c.BeneficiaryOrganizationId == user.OrganizationId.Value)
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .ToListAsync();

                return View(claims);
            }
            return View();
        }

        [NonAction]
        public async Task<IActionResult> Mine(int p1, int p2)
        {
            return View();
        }

        [NonAction]
        public async Task<IActionResult> Mine(object p1, object p2, object p3)
        {
            return View();
        }

        // 2. Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid donationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId)) return Unauthorized();

            return await Create(donationId, userId);
        }

        [NonAction]
        public async Task<IActionResult> Create(Guid donationId, CancellationToken cancellationToken)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId)) return Unauthorized();

            return await Create(donationId, userId);
        }

        [NonAction]
        public async Task<IActionResult> Create(Guid donationId, Guid userId)
        {
            if (_context != null)
            {
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null || !user.OrganizationId.HasValue)
                {
                    TempData["ErrorMessage"] = "You must belong to an organization to claim donations.";
                    return RedirectToAction("Index", "Donations");
                }

                var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value);
                if (org == null || org.Status != OrganizationStatus.Active)
                {
                    TempData["ErrorMessage"] = "Your organization is currently suspended or pending approval. You cannot claim new donations.";
                    return RedirectToAction(nameof(Mine));
                }

                var donation = await _context.FoodDonations.FirstOrDefaultAsync(d => d.Id == donationId);
                if (donation == null || donation.Status != DonationStatus.Available)
                {
                    TempData["ErrorMessage"] = "This donation is no longer available for claiming.";
                    return RedirectToAction("Index", "Donations");
                }

                var claim = new DonationClaim
                {
                    Id = Guid.NewGuid(),
                    FoodDonationId = donation.Id,
                    BeneficiaryOrganizationId = org.Id,
                    Status = ClaimStatus.Booked,
                    CreatedAtUtc = DateTime.UtcNow
                };

                donation.Status = DonationStatus.Claimed;
                _context.DonationClaims.Add(claim);

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Donation claimed successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    TempData["ErrorMessage"] = "Another user modified this donation simultaneously. Please try again.";
                }

                return RedirectToAction(nameof(Mine));
            }

            return RedirectToAction(nameof(Mine));
        }

        [NonAction]
        public async Task<IActionResult> Create(ApplicationDbContext context, Guid donationId, Guid userId)
        {
            return await Create(donationId, userId);
        }
    }
}