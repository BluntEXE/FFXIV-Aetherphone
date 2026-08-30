using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SlotsRulesTests
{
    [Fact]
    public void EveryPaylineCoversEveryReelInsideTheWindow()
    {
        Assert.Equal(SlotsRules.PaylineCount, SlotsRules.Paylines.Length);
        for (var line = 0; line < SlotsRules.Paylines.Length; line++)
        {
            var rows = SlotsRules.Paylines[line];
            Assert.Equal(SlotsRules.ReelCount, rows.Length);
            for (var reel = 0; reel < rows.Length; reel++)
            {
                Assert.InRange(rows[reel], 0, SlotsRules.RowCount - 1);
            }
        }
    }

    [Fact]
    public void PayTableColumnsStartAtTheMinimumLineMatch()
    {
        Assert.Equal(SlotsRules.ReelCount - SlotsRules.MinLineMatch + 1, SlotsRules.LinePays.GetLength(1));
    }
}
