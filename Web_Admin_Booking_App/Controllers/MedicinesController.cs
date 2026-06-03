using Microsoft.AspNetCore.Mvc;
using Web_Admin_Booking_App.Models;
using Web_Admin_Booking_App.Services;

namespace Web_Admin_Booking_App.Controllers;

public sealed class MedicinesController : Controller
{
    private readonly FirestoreAdminDataService _dataService;

    public MedicinesController(FirestoreAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(string? search, string? groupFilter, CancellationToken cancellationToken)
    {
        var model = await _dataService.GetMedicinesAsync(search, groupFilter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = await _dataService.BuildMedicineUpsertModelAsync(new MedicineUpsertViewModel(), cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MedicineUpsertViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model);
        if (!ModelState.IsValid)
        {
            return View(await _dataService.BuildMedicineUpsertModelAsync(model, cancellationToken));
        }

        try
        {
            var id = await _dataService.CreateMedicineAsync(model, CancellationToken.None);
            TempData["SuccessMessage"] = $"Đã thêm thuốc {id}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Không lưu được thuốc vào Firebase: {ex.Message}");
            return View(await _dataService.BuildMedicineUpsertModelAsync(model, cancellationToken));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var model = await _dataService.GetMedicineForEditAsync(id, cancellationToken);
        if (model is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thuốc cần sửa.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MedicineUpsertViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model);
        if (!ModelState.IsValid)
        {
            return View(await _dataService.BuildMedicineUpsertModelAsync(model, cancellationToken));
        }

        try
        {
            await _dataService.UpdateMedicineAsync(model, CancellationToken.None);
            TempData["SuccessMessage"] = "Đã cập nhật thuốc.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Không lưu được thuốc vào Firebase: {ex.Message}");
            return View(await _dataService.BuildMedicineUpsertModelAsync(model, cancellationToken));
        }
    }

    private static void Normalize(MedicineUpsertViewModel model)
    {
        model.Name = model.Name.Trim();
        model.Strength = model.Strength.Trim();
        model.Unit = model.Unit.Trim();
        model.Group = model.Group.Trim();
        model.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
    }
}
