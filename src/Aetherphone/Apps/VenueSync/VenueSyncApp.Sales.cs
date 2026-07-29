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

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var rowCount = salesError is not null ? 5 : 4;
            var card = GroupCard.Begin(theme, rowCount, Metrics.Size.Row);

            var serviceLabel = salesSelectedServiceIndex >= 0 && salesSelectedServiceIndex < salesServices.Count
                ? salesServices[salesSelectedServiceIndex].Name
                : "Select a service";
            if (SettingsRow.Disclosure(card.NextRow(), "Service", serviceLabel, theme) && salesServices.Count > 0)
            {
                salesSelectedServiceIndex = (salesSelectedServiceIndex + 1) % salesServices.Count;
            }

            var customerRow = card.NextRow();
            ImGui.SetCursorScreenPos(new Vector2(customerRow.Min.X,
                customerRow.Center.Y - Metrics.Size.FieldHeight * scale * 0.5f));
            ImGui.SetNextItemWidth(customerRow.Width);
            ImGui.InputTextWithHint("##sales-customer", "Customer (optional)", ref salesCustomerName, 64);

            var amountRow = card.NextRow();
            ImGui.SetCursorScreenPos(new Vector2(amountRow.Min.X,
                amountRow.Center.Y - Metrics.Size.FieldHeight * scale * 0.5f));
            ImGui.SetNextItemWidth(amountRow.Width);
            ImGui.InputTextWithHint("##sales-amount", "Amount (gil)", ref salesAmountText, 12,
                ImGuiInputTextFlags.CharsDecimal);

            if (salesError is not null)
            {
                var errorRow = card.NextRow();
                Marquee.DrawLeftAuto("venuesync.sales.error", $"{salesError} — tap Log Sale to retry", errorRow.Min.X,
                    errorRow.Center.Y - 10f * scale, errorRow.Width, TextStyles.Caption1, theme.Danger);
            }

            if (AppSkin.PillButton(card.NextRow(), "Log Sale", filled: true, theme))
            {
                _ = SubmitSaleAsync();
            }

            card.End();

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            var summaryOrigin = ImGui.GetCursorScreenPos();
            var summaryWidth = ImGui.GetContentRegionAvail().X;
            var summaryRect = new Rect(summaryOrigin,
                new Vector2(summaryOrigin.X + summaryWidth, summaryOrigin.Y + SaleSummaryRow.Height * scale));
            SaleSummaryRow.Draw(summaryRect, state.SessionSalesCount, state.SessionSalesTotal, theme);
        }
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
