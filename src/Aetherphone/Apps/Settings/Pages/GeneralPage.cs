using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class GeneralPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.General);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.SlidersH;
    public Vector4 Tint => new(0.52f, 0.54f, 0.60f, 1f);
    private readonly Configuration configuration;
    private readonly TranslationService translation;
    private readonly ConfirmService confirm;

    public GeneralPage(Configuration configuration, TranslationService translation, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.translation = translation;
        this.confirm = confirm;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var translationRow = translation.Enabled ? 1 : 0;
            var card = GroupCard.Begin(theme, 10 + translationRow);
            var showInGpose = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowInGpose),
                configuration.ShowInGpose, theme, null, Loc.T(L.Settings.ShowInGposeHint));
            var importScreenshots = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ImportScreenshots),
                configuration.ImportScreenshots, theme, null, Loc.T(L.Settings.ImportScreenshotsHint));
            var usesNativeFileDialog = configuration.UseNativeFileDialog ?? NativeFileDialog.IsSupported;
            var nativeFileDialog = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.NativeFileDialog),
                usesNativeFileDialog, theme, null, Loc.T(L.Settings.NativeFileDialogHint));
            var showPhotoChirps = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ChirperShowPhotos),
                configuration.ChirperShowPhotoPosts, theme, null, Loc.T(L.Settings.ChirperShowPhotosHint));
            var showGifChirps = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ChirperShowGifs),
                configuration.ChirperShowGifPosts, theme, null, Loc.T(L.Settings.ChirperShowGifsHint));
            var showReplyMedia = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ChirperShowReplyMedia),
                configuration.ChirperShowCommentMedia, theme, null, Loc.T(L.Settings.ChirperShowReplyMediaHint));
            var showGifGrams = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AethergramShowGifs),
                configuration.AethergramShowGifPosts, theme, null, Loc.T(L.Settings.AethergramShowGifsHint));
            var showCommentMedia = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AethergramShowCommentMedia),
                configuration.AethergramShowCommentMedia, theme, null, Loc.T(L.Settings.AethergramShowCommentMediaHint));
            var showSensitive = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowSensitive),
                configuration.ShowSensitiveContent, theme, null, Loc.T(L.Settings.ShowSensitiveHint));
            var marketContextMenu = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.MarketContextMenu),
                configuration.MarketContextMenu, theme, null, Loc.T(L.Settings.MarketContextMenuHint));
            var autoTranslate = configuration.AutoTranslatePosts;
            if (translationRow > 0)
            {
                autoTranslate = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AutoTranslate),
                    configuration.AutoTranslatePosts, theme, null, Loc.T(L.Settings.AutoTranslateHint));
            }

            card.End();
            if (autoTranslate != configuration.AutoTranslatePosts)
            {
                SetAutoTranslate(autoTranslate);
            }

            if (showInGpose != configuration.ShowInGpose)
            {
                configuration.ShowInGpose = showInGpose;
                Plugin.PluginInterface.UiBuilder.DisableGposeUiHide = showInGpose;
                configuration.Save();
            }

            if (importScreenshots != configuration.ImportScreenshots)
            {
                configuration.ImportScreenshots = importScreenshots;
                configuration.Save();
            }

            if (nativeFileDialog != usesNativeFileDialog)
            {
                configuration.UseNativeFileDialog = nativeFileDialog;
                configuration.Save();
            }

            if (showPhotoChirps != configuration.ChirperShowPhotoPosts)
            {
                configuration.ChirperShowPhotoPosts = showPhotoChirps;
                configuration.Save();
            }

            if (showGifChirps != configuration.ChirperShowGifPosts)
            {
                configuration.ChirperShowGifPosts = showGifChirps;
                configuration.Save();
            }

            if (showReplyMedia != configuration.ChirperShowCommentMedia)
            {
                configuration.ChirperShowCommentMedia = showReplyMedia;
                configuration.Save();
            }

            if (showGifGrams != configuration.AethergramShowGifPosts)
            {
                configuration.AethergramShowGifPosts = showGifGrams;
                configuration.Save();
            }

            if (showCommentMedia != configuration.AethergramShowCommentMedia)
            {
                configuration.AethergramShowCommentMedia = showCommentMedia;
                configuration.Save();
            }

            if (showSensitive != configuration.ShowSensitiveContent)
            {
                configuration.ShowSensitiveContent = showSensitive;
                configuration.Save();
            }

            if (marketContextMenu != configuration.MarketContextMenu)
            {
                configuration.MarketContextMenu = marketContextMenu;
                configuration.Save();
            }

            SettingsSection.Header(Loc.T(L.Settings.Startup), theme);
            var startupCard = GroupCard.Begin(theme, 2);
            var openStartup = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenOnStartup),
                configuration.OpenOnStartup, theme);
            var openMinimized = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenMinimized),
                configuration.OpenMinimizedOnStartup, theme, null, Loc.T(L.Settings.StartupHint));
            startupCard.End();
            if (openStartup != configuration.OpenOnStartup)
            {
                configuration.OpenOnStartup = openStartup;
                configuration.Save();
            }

            if (openMinimized != configuration.OpenMinimizedOnStartup)
            {
                configuration.OpenMinimizedOnStartup = openMinimized;
                configuration.Save();
            }
        }
    }
    private void SetAutoTranslate(bool enabled)
    {
        if (!enabled)
        {
            configuration.AutoTranslatePosts = false;
            configuration.Save();
            return;
        }

        TranslateLink.WithDisclosure(translation, confirm, () =>
        {
            configuration.AutoTranslatePosts = true;
            configuration.Save();
        });
    }
}
