using Newtonsoft.Json;

namespace Aetherphone.Core.Health;

internal sealed class HealthStore
{
    private readonly DirectoryInfo root;

    public HealthStore(DirectoryInfo root)
    {
        this.root = root;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public HealthProfile Load(ulong contentId)
    {
        var path = PathFor(contentId);
        if (!File.Exists(path))
        {
            return new HealthProfile();
        }

        try
        {
            var loaded = JsonConvert.DeserializeObject<HealthProfile>(File.ReadAllText(path));
            return loaded is null ? new HealthProfile() : Sanitize(loaded);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HealthStore load failed for {contentId:X16}: {exception.Message}");
            return new HealthProfile();
        }
    }

    public void Save(ulong contentId, HealthProfile profile)
    {
        if (contentId == 0)
        {
            return;
        }

        try
        {
            var path = PathFor(contentId);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(profile));
            File.Move(temp, path, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HealthStore write failed for {contentId:X16}: {exception.Message}");
        }
    }

    // Guard against corrupt, missing, NaN, infinite, or negative saved values.
    private static HealthProfile Sanitize(HealthProfile profile)
    {
        profile.StrideYalms = profile.StrideYalms is > 0.05 and < 10 ? profile.StrideYalms : 0.75;
        if (profile.WeightKg is { } weight && (!double.IsFinite(weight) || weight <= 0))
        {
            profile.WeightKg = null;
        }

        if (profile.ManualHeightCm is { } height && (!double.IsFinite(height) || height <= 0))
        {
            profile.ManualHeightCm = null;
        }

        profile.DailyStepGoal = Math.Clamp(profile.DailyStepGoal, 1, 1_000_000);
        profile.DailyHydrationGoal = Math.Clamp(profile.DailyHydrationGoal, 1, 100);
        profile.ReminderIntervalMinutes = Math.Clamp(profile.ReminderIntervalMinutes, 1, 720);
        profile.QuietStartHour = Math.Clamp(profile.QuietStartHour, 0, 23);
        profile.QuietEndHour = Math.Clamp(profile.QuietEndHour, 0, 23);
        profile.QuietStartMinute = Math.Clamp(profile.QuietStartMinute, 0, 59);
        profile.QuietEndMinute = Math.Clamp(profile.QuietEndMinute, 0, 59);
        profile.Goals ??= new List<HealthGoal>();
        profile.Days ??= new List<HealthDay>();
        profile.Goals.RemoveAll(goal => goal is null || goal.Target <= 0);
        for (var index = 0; index < profile.Goals.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(profile.Goals[index].Id))
            {
                profile.Goals[index].Id = Guid.NewGuid().ToString("N");
            }
        }

        return profile;
    }

    private string PathFor(ulong contentId) => Path.Combine(root.FullName, contentId.ToString("X16") + ".json");
}
