using System.Text.Json;
using Aetherphone.Core.Mods;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ModsTests
{
    [Theory]
    [InlineData("550de39e-9034-42ca-a150-81e77aa8cea3", "am6y77mg6h1cn8agg7kqna6emc")]
    [InlineData("00000000-0000-0000-0000-000000000000", "00000000000000000000000000")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff", "zzzzzzzzzzzzzzzzzzzzzzzzzw")]
    [InlineData("12345678-9abc-def0-1234-56789abcdef0", "28t5cy4tqkff04hmasw9nf6yy0")]
    public void CrockfordMatchesHeliosphereModUrls(string guid, string expected)
    {
        Assert.Equal(expected, Crockford.Encode(Guid.Parse(guid)));
    }

    [Fact]
    public void ModPageUrlUsesTheCrockfordPackageId()
    {
        var url = ModsContent.ModPageUrl(Guid.Parse("550de39e-9034-42ca-a150-81e77aa8cea3"));

        Assert.Equal("https://heliosphere.app/mod/am6y77mg6h1cn8agg7kqna6emc", url);
    }

    [Theory]
    [InlineData("body-replacement", "Body replacement")]
    [InlineData("hair", "Hair")]
    [InlineData("ui", "UI")]
    [InlineData("vfx", "VFX")]
    [InlineData("", "")]
    public void TagLabelsReadAsWords(string slug, string expected)
    {
        Assert.Equal(expected, ModsContent.TagLabel(slug));
    }

    [Fact]
    public void BlankSearchTextIsOmittedBecauseTheApiReturnsNothingForEmptyNames()
    {
        var info = new ModsQuery("   ", "hair", ModsSort.Popular, ModsFilter.Default).ToInfo();

        Assert.Null(info.Name);
        Assert.Equal(new[] { "hair" }, info.IncludeTags);
        Assert.Equal("DOWNLOADS", info.Order);
    }

    [Fact]
    public void SearchTextIsTrimmedAndCategoryIsOptional()
    {
        var info = new ModsQuery(" gaia ", string.Empty, ModsSort.Trending, ModsFilter.Default).ToInfo();

        Assert.Equal("gaia", info.Name);
        Assert.Empty(info.IncludeTags);
        Assert.Equal("DOWNLOADS_LAST_MONTH", info.Order);
    }

    [Fact]
    public void SnapshotFilterNeedsNsfwBeforeNsflAndInvertsHidePaid()
    {
        var snapshot = new ModsSnapshot { ShowNsfl = true, HidePaid = true };

        var filter = snapshot.Filter;

        Assert.False(filter.Nsfw);
        Assert.False(filter.Nsfl);
        Assert.True(filter.ContentWarnings);
        Assert.False(filter.Paid);

        snapshot.ShowNsfw = true;
        Assert.True(snapshot.Filter.Nsfl);
    }

    [Fact]
    public void SnapshotSortFallsBackToTrendingForUnknownValues()
    {
        Assert.Equal(ModsSort.Trending, new ModsSnapshot { Sort = 99 }.SortKind);
        Assert.Equal(ModsSort.Updated, new ModsSnapshot { Sort = 3 }.SortKind);
    }

    [Fact]
    public void HeliosphereMetaParsesThePascalCaseFileHeliosphereWrites()
    {
        const string json = """
            {
              "MetaVersion": 4,
              "Id": "550de39e-9034-42ca-a150-81e77aa8cea3",
              "Name": "Hair Defined 2 - Ultra",
              "Tagline": "HD vanilla hair upgrades since 2018",
              "Description": "long text",
              "Author": "Kylie",
              "AuthorId": "11111111-1111-1111-1111-111111111111",
              "Version": "7.1.0",
              "VersionId": "7178fd61-f598-4d95-a5a0-33b7f5836cd0",
              "Variant": "UHD 4x",
              "VariantId": "dbe12582-ddf9-4d0e-802f-3323528cfa71",
              "ShortVariantId": 12,
              "IncludeTags": true,
              "FileStorageMethod": 0
            }
            """;

        var meta = JsonSerializer.Deserialize(json, HeliosphereJsonContext.Default.HeliosphereMeta);

        Assert.NotNull(meta);
        Assert.Equal(4u, meta!.MetaVersion);
        Assert.Equal(Guid.Parse("550de39e-9034-42ca-a150-81e77aa8cea3"), meta.Id);
        Assert.Equal("UHD 4x", meta.Variant);
        Assert.Equal(Guid.Parse("7178fd61-f598-4d95-a5a0-33b7f5836cd0"), meta.VersionId);
        Assert.Equal("7.1.0", meta.Version);
    }

    [Fact]
    public void SearchEnvelopeBindsCardDataAndBuildsThumbnails()
    {
        const string json = """
            {"data":{"searchVersions":{"pageInfo":{"prev":false,"next":true,"total":1250},"versions":[{"id":"b9fcd567-ec46-442b-83d1-64b36e59282a","variantId":"dbe12582-ddf9-4d0e-802f-3323528cfa71","version":"7.1.0","createdAt":"2024-12-25T01:02:13.144055+00:00","variant":{"id":"dbe12582-ddf9-4d0e-802f-3323528cfa71","name":"UHD 4x","shortId":12,"displayOrder":0,"package":{"id":"550de39e-9034-42ca-a150-81e77aa8cea3","name":"Hair Defined 2 - Ultra","tagline":"HD vanilla hair upgrades since 2018","downloads":42375,"updatedAt":"2025-07-07T14:28:58.662273+00:00","user":{"id":"22222222-2222-2222-2222-222222222222","username":"kylie","visibleName":"Kylie","subscriber":true},"tags":[{"slug":"eyebrows","category":false},{"slug":"hair","category":true}],"nsfw":{"nsfw":false,"nsfl":false,"cw":false},"images":[{"id":2,"hash":"second","displayOrder":1},{"id":1,"hash":"first","displayOrder":0}]}}}]}}}
            """;

        var envelope = JsonSerializer.Deserialize(json, HeliosphereJsonContext.Default.SearchEnvelope);
        var result = envelope?.Data?.SearchVersions;

        Assert.NotNull(result);
        Assert.True(result!.PageInfo.Next);
        Assert.Equal(1250, result.PageInfo.Total);
        var card = ModCardModel.From(result.Versions[0]);
        Assert.NotNull(card);
        Assert.Equal("Kylie", card!.Author);
        Assert.Equal("Hair", card.CategoryLabel);
        Assert.Equal("42.4K", card.DownloadsText);
        Assert.Equal("https://edge.heliosphere.app/images/thumbnails/first/256", card.ThumbnailUrl);
        Assert.False(card.Sensitive);
    }

    [Fact]
    public void GraphQlErrorsSurviveBinding()
    {
        const string json = """{"data":null,"errors":[{"message":"Failed to parse \"UUID\""}]}""";

        var envelope = JsonSerializer.Deserialize(json, HeliosphereJsonContext.Default.PackageEnvelope);

        Assert.NotNull(envelope);
        Assert.Null(envelope!.Data);
        Assert.Single(envelope.Errors!);
    }


    [Fact]
    public void InstallRequestSerializesCamelCaseAndDropsAnEmptyPassword()
    {
        var request = new BridgeInstallRequest
        {
            PackageId = Guid.Parse("550de39e-9034-42ca-a150-81e77aa8cea3"),
            VariantId = Guid.Parse("dbe12582-ddf9-4d0e-802f-3323528cfa71"),
            VersionId = Guid.Parse("7178fd61-f598-4d95-a5a0-33b7f5836cd0"),
        };

        var json = JsonSerializer.Serialize(request, HeliosphereJsonContext.Default.BridgeInstallRequest);

        Assert.Contains("\"packageId\":\"550de39e-9034-42ca-a150-81e77aa8cea3\"", json);
        Assert.DoesNotContain("oneClickPassword", json);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(2897, "2,897")]
    [InlineData(42375, "42.4K")]
    [InlineData(1_250_000, "1.3M")]
    public void DownloadCountsStayShort(int count, string expected)
    {
        Assert.Equal(expected, ModsContent.FormatCount(count));
    }
}
