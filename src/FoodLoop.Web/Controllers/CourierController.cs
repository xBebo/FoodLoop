using System;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Courier;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers;

[Authorize]
public sealed class CourierController(TaskDetailsService taskDetailsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var task = await taskDetailsService.GetTaskDetailsAsync(id, ct);
        if (task == null)
        {
            TempData["ErrorMessage"] = "Task not found or access denied.";
            return RedirectToAction("Index", "Home");
        }

        return View(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(Guid id, HandoverType actionType, CancellationToken ct)
    {
        var error = await taskDetailsService.AdvanceTaskStatusAsync(id, actionType, ct);
        if (error != null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Task status updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }
}