using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private string? salesServicesLoadedForVenueId;
    private List<VenueSyncService> salesServices = new();
    private int salesSelectedServiceIndex = -1;
    private bool salesServicePickerExpanded;
    private string salesCustomerName = string.Empty;
    private string salesAmountText = string.Empty;
    private string? salesError;

    private void DrawSales(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.VenueSync.LogSale), back);

        if (salesServicesLoadedForVenueId != configuration.VenueSyncSelectedVenueId)
        {
            salesServicesLoadedForVenueId = configuration.VenueSyncSelectedVenueId;
            salesSelectedServiceIndex = -1;
            salesServicePickerExpanded = false;
            if (string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueId))
            {
                salesServices = new();
            }
            else
            {
                _ = LoadSalesServicesAsync();
            }
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var rowCount = (salesError is not null ? 5 : 4) + (salesServicePickerExpanded ? salesServices.Count : 0);
            var card = GroupCard.Begin(theme, rowCount, Metrics.Size.Row);

            var serviceLabel = salesSelectedServiceIndex >= 0 && salesSelectedServiceIndex < salesServices.Count
                ? salesServices[salesSelectedServiceIndex].Name
                : Loc.T(L.VenueSync.SelectService);
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.VenueSync.ServiceLabel), serviceLabel, theme))
            {
                if (salesServices.Count == 0)
                {
                    _ = LoadSalesServicesAsync();
                }

                salesServicePickerExpanded = !salesServicePickerExpanded;
            }

            if (salesServicePickerExpanded)
            {
                for (var i = 0; i < salesServices.Count; i++)
                {
                    var service = salesServices[i];
                    var selected = i == salesSelectedServiceIndex;
                    if (SettingsRow.Selectable(card.NextRow(), service.Name, selected, theme,
                            $"venuesync.sales.service-{service.Id}"))
                    {
                        salesSelectedServiceIndex = i;
                        salesServicePickerExpanded = false;
                    }
                }
            }

            var customerRow = card.NextRow();
            var targetButtonWidth = 36f * scale;
            var customerFieldWidth = MathF.Max(1f,
                customerRow.Width - targetButtonWidth - Metrics.Space.Sm * scale);
            var fieldHeight = Metrics.Size.FieldHeight * scale;
            var customerFieldRect = new Rect(
                new Vector2(customerRow.Min.X, customerRow.Center.Y - fieldHeight * 0.5f),
                new Vector2(customerRow.Min.X + customerFieldWidth, customerRow.Center.Y + fieldHeight * 0.5f));
            DrawStyledField(customerFieldRect, "##sales-customer", Loc.T(L.VenueSync.CustomerHint),
                ref salesCustomerName, 64);

            var targetButtonCenter = new Vector2(customerRow.Max.X - targetButtonWidth * 0.5f, customerRow.Center.Y);
            if (AppSkin.IconButton(targetButtonCenter, targetButtonWidth * 0.5f,
                    FontAwesomeIcon.Crosshairs.ToIconString(), theme.TextMuted, theme.GroupedCard, 0.6f, theme,
                    Loc.T(L.VenueSync.UseCurrentTarget)))
            {
                var name = Plugin.TargetManager.Target?.Name.TextValue;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    salesCustomerName = name;
                }
            }

            var amountRow = card.NextRow();
            var amountFieldRect = new Rect(new Vector2(amountRow.Min.X, amountRow.Center.Y - fieldHeight * 0.5f),
                new Vector2(amountRow.Max.X, amountRow.Center.Y + fieldHeight * 0.5f));
            DrawStyledField(amountFieldRect, "##sales-amount", Loc.T(L.VenueSync.AmountHint), ref salesAmountText, 12,
                ImGuiInputTextFlags.CharsDecimal);

            if (salesError is not null)
            {
                var errorRow = card.NextRow();
                Marquee.DrawLeftAuto("venuesync.sales.error", Loc.T(L.VenueSync.RetryLogSaleHint, salesError),
                    errorRow.Min.X, errorRow.Center.Y - 10f * scale, errorRow.Width, TextStyles.Caption1,
                    theme.Danger);
            }

            var submitRow = card.NextRow();
            var submitButtonHeight = Metrics.Size.FieldHeight * scale;
            var submitButtonRect = new Rect(new Vector2(submitRow.Min.X, submitRow.Center.Y - submitButtonHeight * 0.5f),
                new Vector2(submitRow.Max.X, submitRow.Center.Y + submitButtonHeight * 0.5f));
            if (AppSkin.PillButton(submitButtonRect, Loc.T(L.VenueSync.LogSaleButton), filled: true, theme))
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
            salesError = Loc.T(L.VenueSync.EnterValidAmount);
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
                salesError = result?.Error ?? Loc.T(L.VenueSync.FailedLogSale);
            }
        }
        catch (Exception exception)
        {
            salesError = exception.Message;
        }
    }
}
