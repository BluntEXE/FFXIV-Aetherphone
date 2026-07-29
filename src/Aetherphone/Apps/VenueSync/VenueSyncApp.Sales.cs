using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private bool salesServicesLoaded;
    private List<VenueSyncService> salesServices = new();
    private int salesSelectedServiceIndex = -1;
    private string salesCustomerName = string.Empty;
    private string salesAmountText = string.Empty;
    private string? salesError;

    private void DrawSales(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), "Log a Sale", back);

        if (!salesServicesLoaded)
        {
            salesServicesLoaded = true;
            _ = LoadSalesServicesAsync();
        }

        var cursorY = area.Min.Y + AppHeader.Height * scale;
        var left = area.Min.X + Metrics.Space.Lg * scale;
        var right = area.Max.X - Metrics.Space.Lg * scale;
        var width = MathF.Max(1f, right - left);

        var serviceLabel = salesSelectedServiceIndex >= 0 && salesSelectedServiceIndex < salesServices.Count
            ? salesServices[salesSelectedServiceIndex].Name
            : "Select a service";
        var serviceRow = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + Metrics.Size.Row * scale));
        if (SettingsRow.Disclosure(serviceRow, "Service", serviceLabel, theme) && salesServices.Count > 0)
        {
            salesSelectedServiceIndex = (salesSelectedServiceIndex + 1) % salesServices.Count;
        }

        cursorY += Metrics.Size.Row * scale + Metrics.Space.Sm * scale;

        ImGui.SetCursorScreenPos(new Vector2(left, cursorY));
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##sales-customer", "Customer (optional)", ref salesCustomerName, 64);
        cursorY += Metrics.Size.FieldHeight * scale + Metrics.Space.Sm * scale;

        ImGui.SetCursorScreenPos(new Vector2(left, cursorY));
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##sales-amount", "Amount (gil)", ref salesAmountText, 12,
            ImGuiInputTextFlags.CharsDecimal);
        cursorY += Metrics.Size.FieldHeight * scale + Metrics.Space.Md * scale;

        if (salesError is not null)
        {
            Marquee.DrawLeftAuto("venuesync.sales.error", $"{salesError} — tap Log Sale to retry", left, cursorY,
                width, TextStyles.Caption1, theme.Danger);
            cursorY += 20f * scale + Metrics.Space.Sm * scale;
        }

        var submitRow = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + Metrics.Size.Row * scale));
        if (SettingsRow.Action(submitRow, "Log Sale", theme.Accent, theme))
        {
            _ = SubmitSaleAsync();
        }

        cursorY += Metrics.Size.Row * scale + Metrics.Space.Lg * scale;

        var summaryRect = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + SaleSummaryRow.Height * scale));
        SaleSummaryRow.Draw(summaryRect, state.SessionSalesCount, state.SessionSalesTotal, theme);
    }

    private async Task LoadSalesServicesAsync()
    {
        var response = await client.GetServicesAsync(configuration.VenueSyncSelectedVenueId, CancellationToken.None)
            .ConfigureAwait(false);
        if (response is not null)
        {
            salesServices = response.Services;
        }
    }

    private async Task SubmitSaleAsync()
    {
        if (!decimal.TryParse(salesAmountText, out var amount) || amount <= 0)
        {
            salesError = "Enter a valid amount";
            return;
        }

        var selectedService = salesSelectedServiceIndex >= 0 && salesSelectedServiceIndex < salesServices.Count
            ? salesServices[salesSelectedServiceIndex]
            : null;
        var request = new VenueSyncTransactionRequest
        {
            VenueId = configuration.VenueSyncSelectedVenueId,
            ServiceId = selectedService?.Id,
            Amount = amount,
            CustomerName = string.IsNullOrWhiteSpace(salesCustomerName) ? null : salesCustomerName,
        };

        try
        {
            var result = await client.LogTransactionAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (result is { Success: true })
            {
                salesError = null;
                state.RecordSale(amount);
                salesAmountText = string.Empty;
            }
            else
            {
                salesError = result?.Error ?? "Failed to log sale";
            }
        }
        catch (Exception exception)
        {
            salesError = exception.Message;
        }
    }
}
