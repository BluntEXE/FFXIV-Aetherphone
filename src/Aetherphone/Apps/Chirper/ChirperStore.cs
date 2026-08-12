using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperStore : SocialFeedStore
{
    public const int MaxImages = 4;

    public const long MaxGifBytes = 4L * 1024 * 1024;

    private const int MaxImageDimension = 1600;

    private const string UploadScope = "chirp";

    public static bool IsGifPath(string path)
    {
        return path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private volatile bool avatarBusy;

    public ChirperStore(AethernetSession session, AccountClient account, SocialClient client, SafetyClient safety,
        MediaClient media, RealtimeSignalBus signals)
        : base(session, account, client, safety, media, signals, "Chirper")
    {
    }

    public bool AvatarBusy => avatarBusy;

    protected override Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        client.FeedAsync(feedKey, cursor, token, onFailure);

    protected override Task<FeedPage?> FetchProfilePostsAsync(string userId, string? cursor, CancellationToken token) =>
        client.UserPostsAsync(userId, cursor, token);

    public void Compose(string text, IReadOnlyList<string> imagePaths, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        var trimmed = text.Trim();
        if ((trimmed.Length == 0 && imagePaths.Count == 0) || posting)
        {
            return;
        }

        posting = true;
        work.Run("compose", async token =>
        {
            var uploaded = await UploadImagesAsync(imagePaths, token, onFailure).ConfigureAwait(false);
            if (uploaded is null)
            {
                return false;
            }

            var (keys, width, height) = uploaded.Value;
            var created = await client.CreatePostAsync(trimmed, keys.Length > 0 ? keys : null, width, height, token,
                    onFailure)
                .ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            AcceptCreatedPost(created);
            return true;
        }, onComplete, () => posting = false);
    }

    private async Task<(string[] Keys, int Width, int Height)?> UploadImagesAsync(
        IReadOnlyList<string> imagePaths, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        if (imagePaths.Count == 0)
        {
            return (Array.Empty<string>(), 0, 0);
        }

        var keys = new string[Math.Min(imagePaths.Count, MaxImages)];
        var firstWidth = 0;
        var firstHeight = 0;
        for (var index = 0; index < keys.Length; index++)
        {
            byte[] bytes;
            string contentType;
            int width;
            int height;
            if (IsGifPath(imagePaths[index]))
            {
                bytes = await File.ReadAllBytesAsync(imagePaths[index], token).ConfigureAwait(false);
                if (bytes.Length == 0 || bytes.Length > MaxGifBytes)
                {
                    AepLog.Warning($"Chirper upload rejected a GIF of {bytes.Length} bytes; the cap is {MaxGifBytes}");
                    onFailure?.Invoke(new AepFailure(AepFailureKind.Server, 0, FailureCodes.MediaInvalidImage, null,
                        null, null));
                    return null;
                }

                (width, height) = ImageProcessor.IdentifyDimensions(bytes);
                contentType = "image/gif";
            }
            else
            {
                var baked = ImageProcessor.BakeJpeg(imagePaths[index], MaxImageDimension);
                bytes = baked.Bytes;
                width = baked.Width;
                height = baked.Height;
                contentType = "image/jpeg";
            }

            var upload = await media.UploadUrlAsync(contentType, UploadScope, token, onFailure).ConfigureAwait(false);
            if (upload is null)
            {
                return null;
            }

            var sent = await media.UploadImageAsync(upload.UploadUrl, bytes, contentType, token)
                .ConfigureAwait(false);
            if (!sent)
            {
                AepLog.Warning($"Chirper upload could not store image {index + 1} of {keys.Length}");
                onFailure?.Invoke(AepFailure.Transport(AepFailureKind.Offline));
                return null;
            }

            keys[index] = upload.Key;
            if (index == 0)
            {
                firstWidth = width;
                firstHeight = height;
            }
        }

        return (keys, firstWidth, firstHeight);
    }

    public void ToggleReaction(PostDto post, int kind)
    {
        var target = post.MyReaction == kind ? -1 : kind;
        ReplacePost(ApplyReaction(post, target));
        work.Run("reaction", async token =>
        {
            var result = target < 0
                ? await client.RemoveReactionAsync(post.Id, token).ConfigureAwait(false)
                : await client.ReactAsync(post.Id, target, token).ConfigureAwait(false);
            if (result is not null)
            {
                ReplacePost(result);
            }
        });
    }

    public void ToggleRepost(PostDto post)
    {
        var original = post.RepostOfId is not null && post.ReferencedPost is not null ? post.ReferencedPost : post;
        var next = !original.MyReposted;
        var updated = original with
        {
            MyReposted = next,
            RepostCount = Math.Max(0, original.RepostCount + (next ? 1 : -1)),
        };
        ReplacePost(updated);
        if (next)
        {
            InsertLocalRepost(updated);
        }
        else
        {
            RemoveLocalReposts(updated.Id);
        }

        work.Run("repost", async token =>
        {
            var result = next
                ? await client.RepostAsync(original.Id, token).ConfigureAwait(false)
                : await client.UnrepostAsync(original.Id, token).ConfigureAwait(false);
            if (result is not null)
            {
                ReplacePost(result);
            }
        });
    }

    private void InsertLocalRepost(PostDto original)
    {
        if (Me is not { } me)
        {
            return;
        }

        RemoveLocalReposts(original.Id);
        var row = original with
        {
            Id = "pending-repost-" + original.Id,
            AuthorId = me.Id,
            AuthorName = me.Name,
            AuthorWorld = me.World,
            AuthorDisplayName = me.DisplayName,
            AuthorHandle = me.Handle,
            AuthorAvatarUrl = me.AvatarUrl,
            Text = string.Empty,
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            RepostOfId = original.Id,
            ReferencedPost = original,
        };
        AcceptCreatedPost(row);
    }

    private void RemoveLocalReposts(string originalId)
    {
        if (Me is not { } me)
        {
            return;
        }

        forYouLane.Items = CopyOnWrite.RemoveWhere(forYouLane.Items,
            post => post.RepostOfId == originalId && post.AuthorId == me.Id);
        followingLane.Items = CopyOnWrite.RemoveWhere(followingLane.Items,
            post => post.RepostOfId == originalId && post.AuthorId == me.Id);
        profileLane.Items = CopyOnWrite.RemoveWhere(profileLane.Items,
            post => post.RepostOfId == originalId && post.AuthorId == me.Id);
    }

    public void Quote(string text, string quotedPostId, IReadOnlyList<string> imagePaths, Action<bool> onComplete)
    {
        var trimmed = text.Trim();
        if ((trimmed.Length == 0 && imagePaths.Count == 0) || posting)
        {
            return;
        }

        posting = true;
        work.Run("quote", async token =>
        {
            var uploaded = await UploadImagesAsync(imagePaths, token).ConfigureAwait(false);
            if (uploaded is null)
            {
                return false;
            }

            var (keys, width, height) = uploaded.Value;
            var created = await client.QuotePostAsync(trimmed, quotedPostId, keys.Length > 0 ? keys : null, width, height, token)
                .ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            AcceptCreatedPost(created);
            return true;
        }, onComplete, () => posting = false);
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        work.Run("avatar update", token => UploadAvatarAsync(sourcePath, crop, token), onComplete,
            () => avatarBusy = false);
    }

    private static PostDto ApplyReaction(PostDto post, int newKind)
    {
        var (counts, total) = ReactionTally.Shift(post.ReactionCounts, post.MyReaction, newKind);
        return post with { ReactionCounts = counts, TotalReactions = total, MyReaction = newKind };
    }
}
