using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Solitaire;

internal sealed class SolitaireRenderer
{
    private static readonly Vector4 FoundationGhost = new(1f, 1f, 1f, 0.12f);
    private static readonly Vector4 StockRing = new(1f, 1f, 1f, 0.3f);

    public void Draw(SolitaireBoard board, in SolitaireLayout layout, PhoneTheme theme, Vector4 accent, float scale,
        in SolitaireHit dragSource, in SolitaireHit dropTarget)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = PlayingCards.RoundingFor(layout.CardWidth);
        DrawStock(drawList, board, layout, rounding, scale);
        DrawWaste(drawList, board, layout, rounding, scale, dragSource);
        DrawFoundations(drawList, board, layout, rounding, scale, dragSource);
        DrawTableau(drawList, board, layout, rounding, scale, dragSource);
        if (dropTarget.Kind != SolitairePileKind.None)
        {
            DrawDropHighlight(drawList, layout, rounding, accent, scale, dropTarget);
        }
    }

    private void DrawStock(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale)
    {
        var rect = layout.StockRect;
        if (board.StockCount == 0)
        {
            PlayingCards.DrawSlot(drawList, rect, rounding, scale);
            var center = rect.Center;
            var radius = layout.CardWidth * 0.24f;
            drawList.AddCircle(center, radius, ImGui.GetColorU32(StockRing), 24, 2f * scale);
            return;
        }

        PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
    }

    private void DrawWaste(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale, in SolitaireHit dragSource)
    {
        var rect = layout.WasteRect;
        var fromTop = dragSource.Kind == SolitairePileKind.Waste ? 1 : 0;
        var card = board.WastePeek(fromTop);
        if (card < 0)
        {
            PlayingCards.DrawSlot(drawList, rect, rounding, scale);
            return;
        }

        PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
    }

    private void DrawFoundations(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout,
        float rounding, float scale, in SolitaireHit dragSource)
    {
        for (var suit = 0; suit < SolitaireBoard.SuitCount; suit++)
        {
            var rect = layout.FoundationRect(suit);
            var fromTop = dragSource.Kind == SolitairePileKind.Foundation && dragSource.Pile == suit ? 1 : 0;
            var card = board.FoundationPeek(suit, fromTop);
            if (card < 0)
            {
                PlayingCards.DrawSlot(drawList, rect, rounding, scale);
                PlayingCards.DrawSuit(drawList, rect.Center, layout.CardWidth * 0.26f, suit, FoundationGhost);
                continue;
            }

            PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
        }
    }

    private void DrawTableau(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale, in SolitaireHit dragSource)
    {
        for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
        {
            var count = board.TableauCount(pile);
            if (count == 0)
            {
                PlayingCards.DrawSlot(drawList, layout.TableauBaseRect(pile), rounding, scale);
                continue;
            }

            var skipFrom = dragSource.Kind == SolitairePileKind.Tableau && dragSource.Pile == pile
                ? dragSource.CardIndex
                : count;
            for (var index = 0; index < count; index++)
            {
                if (index >= skipFrom)
                {
                    break;
                }

                var rect = layout.TableauCardRect(pile, index);
                if (board.IsTableauFaceUp(pile, index))
                {
                    PlayingCards.DrawFace(drawList, rect, board.TableauCardAt(pile, index), rounding, scale, true);
                }
                else
                {
                    PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
                }
            }
        }
    }

    public void DrawFloating(in SolitaireLayout layout, ReadOnlySpan<int> cards, Vector2 topLeft, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = PlayingCards.RoundingFor(layout.CardWidth);
        for (var index = 0; index < cards.Length; index++)
        {
            var min = new Vector2(topLeft.X, topLeft.Y + index * layout.FanUp);
            var rect = new Rect(min, min + layout.CardSize);
            Elevation.Floating(drawList, rect.Min, rect.Max, rounding, scale, 0.6f);
            PlayingCards.DrawFace(drawList, rect, cards[index], rounding, scale, false);
        }
    }

    private void DrawDropHighlight(ImDrawListPtr drawList, in SolitaireLayout layout, float rounding, Vector4 accent,
        float scale, in SolitaireHit target)
    {
        var rect = target.Kind switch
        {
            SolitairePileKind.Foundation => layout.FoundationRect(target.Pile),
            SolitairePileKind.Tableau => target.CardIndex >= 0
                ? layout.TableauCardRect(target.Pile, target.CardIndex)
                : layout.TableauBaseRect(target.Pile),
            _ => layout.TopSlot(0),
        };
        Squircle.Stroke(drawList, rect.Min - new Vector2(2f * scale, 2f * scale),
            rect.Max + new Vector2(2f * scale, 2f * scale), rounding + 2f * scale, ImGui.GetColorU32(accent),
            2.4f * scale);
    }
}
