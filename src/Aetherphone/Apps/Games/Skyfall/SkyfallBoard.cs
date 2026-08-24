namespace Aetherphone.Apps.Games.Skyfall;

internal struct Meteor
{
    public Vector2 Start;
    public Vector2 Position;
    public Vector2 Direction;
    public float Speed;
    public bool CanSplit;
    public float SplitY;
}

internal struct Interceptor
{
    public Vector2 Position;
    public Vector2 Target;
    public Vector2 Direction;
}

internal struct Blast
{
    public Vector2 Center;
    public float Radius;
    public bool Growing;
    public float Hold;
}

internal sealed class SkyfallBoard
{
    public const float Width = 100f;
    public const float Height = 140f;
    public const float GroundY = 130f;
    public const float BatteryX = Width * 0.5f;
    public const float BarrelY = GroundY - 3f;
    public const int CityCount = 6;
    public const float CityHalfWidth = 5f;
    public const int AmmoPerWave = 28;
    public const int MeteorPoints = 25;
    public const int CityBonus = 100;
    public const int AmmoBonus = 5;
    public const float BlastMaxRadius = 8.5f;
    public const float WaveBreakSeconds = 2.2f;
    public const int MaxMeteors = 64;
    public const int MaxInterceptors = 32;
    public const int MaxBlasts = 96;
    public static readonly float[] CityX = { 9f, 22f, 35f, 65f, 78f, 91f };
    private const float ShotSpeed = 110f;
    private const float BlastGrowth = 30f;
    private const float BlastHold = 0.22f;
    private const float BlastShrink = BlastGrowth * 0.75f;
    private const float BlastStartRadius = 0.5f;
    private const int MaxMeteorsPerWave = 26;
    private const float MaxMeteorSpeed = 22f;
    private const int SplitFromWave = 3;
    private const float SplitChance = 0.25f;
    private const float BatteryTargetChance = 0.15f;
    private const float FirstSpawnDelay = 0.6f;
    private const float MeteorSpawnY = -4f;
    private const float MinimumShotDistanceSquared = 1f;
    private readonly bool[] cities = new bool[CityCount];
    private readonly Meteor[] meteors = new Meteor[MaxMeteors];
    private readonly Interceptor[] interceptors = new Interceptor[MaxInterceptors];
    private readonly Blast[] blasts = new Blast[MaxBlasts];
    private readonly Vector2[] destroyedPositions = new Vector2[MaxMeteors];
    private readonly Vector2[] blastSpawnPositions = new Vector2[MaxBlasts];
    private readonly Random random = new();
    private int pendingSpawns;
    private float spawnTimer;
    private float waveBreak;
    public int Score { get; private set; }
    public int Wave { get; private set; }
    public int Ammo { get; private set; }
    public int CitiesLeft { get; private set; }
    public bool GameOver { get; private set; }
    public int LastWaveBonus { get; private set; }
    public int MeteorCount { get; private set; }
    public int InterceptorCount { get; private set; }
    public int BlastCount { get; private set; }
    public int DestroyedCount { get; private set; }
    public int BlastSpawnCount { get; private set; }
    public int CityLostThisFrame { get; private set; } = -1;
    public bool WaveStartedThisFrame { get; private set; }
    public bool WaveClearedThisFrame { get; private set; }
    public bool ShotFiredThisFrame { get; private set; }
    public bool DryFireThisFrame { get; private set; }
    public bool InWaveBreak => waveBreak > 0f;
    public bool CityAlive(int index) => cities[index];
    public Meteor GetMeteor(int index) => meteors[index];
    public Interceptor GetInterceptor(int index) => interceptors[index];
    public Blast GetBlast(int index) => blasts[index];
    public Vector2 DestroyedPosition(int index) => destroyedPositions[index];
    public Vector2 BlastSpawnPosition(int index) => blastSpawnPositions[index];

    public static Vector2 CityCenter(int index) => new(CityX[index], GroundY);

    public void StartGame()
    {
        Score = 0;
        Wave = 0;
        GameOver = false;
        CitiesLeft = CityCount;
        for (var cityIndex = 0; cityIndex < CityCount; cityIndex++)
        {
            cities[cityIndex] = true;
        }

        MeteorCount = 0;
        InterceptorCount = 0;
        BlastCount = 0;
        waveBreak = 0f;
        LastWaveBonus = 0;
        ClearFrameEvents();
        StartWave();
    }

    public bool Fire(Vector2 target)
    {
        if (GameOver || InWaveBreak)
        {
            return false;
        }

        if (Ammo <= 0)
        {
            DryFireThisFrame = true;
            return false;
        }

        if (target.Y > BarrelY || InterceptorCount >= MaxInterceptors)
        {
            return false;
        }

        var origin = new Vector2(BatteryX, BarrelY);
        var offset = target - origin;
        if (offset.LengthSquared() < MinimumShotDistanceSquared)
        {
            return false;
        }

        Ammo--;
        interceptors[InterceptorCount++] = new Interceptor
        {
            Position = origin,
            Target = target,
            Direction = Vector2.Normalize(offset),
        };
        ShotFiredThisFrame = true;
        return true;
    }

    public void Update(float deltaSeconds)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        if (waveBreak > 0f)
        {
            waveBreak -= deltaSeconds;
            if (waveBreak <= 0f)
            {
                waveBreak = 0f;
                StartWave();
            }

            return;
        }

        Spawn(deltaSeconds);
        MoveInterceptors(deltaSeconds);
        MoveBlasts(deltaSeconds);
        MoveMeteors(deltaSeconds);
        if (!GameOver && pendingSpawns == 0 && MeteorCount == 0 && InterceptorCount == 0 && BlastCount == 0)
        {
            CompleteWave();
        }
    }

    public static int MeteorsForWave(int wave) => Math.Min(MaxMeteorsPerWave, 7 + wave * 2);

    public static int WaveBonus(int citiesLeft, int ammo) => citiesLeft * CityBonus + ammo * AmmoBonus;

    private void ClearFrameEvents()
    {
        DestroyedCount = 0;
        BlastSpawnCount = 0;
        CityLostThisFrame = -1;
        WaveStartedThisFrame = false;
        WaveClearedThisFrame = false;
        ShotFiredThisFrame = false;
        DryFireThisFrame = false;
    }

    private void StartWave()
    {
        Wave++;
        Ammo = AmmoPerWave;
        pendingSpawns = MeteorsForWave(Wave);
        spawnTimer = FirstSpawnDelay;
        WaveStartedThisFrame = true;
    }

    private void CompleteWave()
    {
        LastWaveBonus = WaveBonus(CitiesLeft, Ammo);
        Score += LastWaveBonus;
        WaveClearedThisFrame = true;
        waveBreak = WaveBreakSeconds;
    }

    private void Spawn(float deltaSeconds)
    {
        if (pendingSpawns <= 0)
        {
            return;
        }

        spawnTimer -= deltaSeconds;
        if (spawnTimer > 0f || MeteorCount >= MaxMeteors)
        {
            return;
        }

        spawnTimer = MathF.Max(0.35f, 1.5f - Wave * 0.08f) * (0.6f + Chance() * 0.8f);
        pendingSpawns--;
        var start = new Vector2(Chance() * Width, MeteorSpawnY);
        var target = PickTarget();
        meteors[MeteorCount++] = new Meteor
        {
            Start = start,
            Position = start,
            Direction = Vector2.Normalize(target - start),
            Speed = MathF.Min(MaxMeteorSpeed, 7f + Wave * 1.3f) * (0.85f + Chance() * 0.3f),
            CanSplit = Wave >= SplitFromWave && Chance() < SplitChance,
            SplitY = 40f + Chance() * 30f,
        };
    }

    private Vector2 PickTarget()
    {
        if (CitiesLeft == 0 || Chance() < BatteryTargetChance)
        {
            return new Vector2(BatteryX, GroundY);
        }

        var pick = random.Next(CitiesLeft);
        for (var cityIndex = 0; cityIndex < CityCount; cityIndex++)
        {
            if (!cities[cityIndex])
            {
                continue;
            }

            if (pick == 0)
            {
                return CityCenter(cityIndex);
            }

            pick--;
        }

        return new Vector2(BatteryX, GroundY);
    }

    private void MoveInterceptors(float deltaSeconds)
    {
        var step = ShotSpeed * deltaSeconds;
        for (var index = InterceptorCount - 1; index >= 0; index--)
        {
            ref var shot = ref interceptors[index];
            if (Vector2.Distance(shot.Position, shot.Target) <= step)
            {
                SpawnBlast(shot.Target);
                interceptors[index] = interceptors[--InterceptorCount];
                continue;
            }

            shot.Position += shot.Direction * step;
        }
    }

    private void MoveBlasts(float deltaSeconds)
    {
        for (var index = BlastCount - 1; index >= 0; index--)
        {
            ref var blast = ref blasts[index];
            if (blast.Growing)
            {
                blast.Radius += BlastGrowth * deltaSeconds;
                if (blast.Radius >= BlastMaxRadius)
                {
                    blast.Radius = BlastMaxRadius;
                    blast.Growing = false;
                    blast.Hold = BlastHold;
                }
            }
            else if (blast.Hold > 0f)
            {
                blast.Hold -= deltaSeconds;
            }
            else
            {
                blast.Radius -= BlastShrink * deltaSeconds;
                if (blast.Radius <= 0f)
                {
                    blasts[index] = blasts[--BlastCount];
                    continue;
                }
            }

            SweepMeteors(blast.Center, blast.Radius);
        }
    }

    private void SweepMeteors(Vector2 center, float radius)
    {
        var radiusSquared = radius * radius;
        for (var index = MeteorCount - 1; index >= 0; index--)
        {
            var position = meteors[index].Position;
            if (Vector2.DistanceSquared(position, center) > radiusSquared)
            {
                continue;
            }

            Score += MeteorPoints;
            if (DestroyedCount < destroyedPositions.Length)
            {
                destroyedPositions[DestroyedCount++] = position;
            }

            meteors[index] = meteors[--MeteorCount];
        }
    }

    private void MoveMeteors(float deltaSeconds)
    {
        for (var index = MeteorCount - 1; index >= 0; index--)
        {
            ref var meteor = ref meteors[index];
            meteor.Position += meteor.Direction * meteor.Speed * deltaSeconds;
            if (meteor.CanSplit && meteor.Position.Y >= meteor.SplitY)
            {
                meteor.CanSplit = false;
                SplitMeteor(meteor.Position, meteor.Speed);
            }

            if (meteor.Position.Y < GroundY)
            {
                continue;
            }

            var impactX = meteor.Position.X;
            meteors[index] = meteors[--MeteorCount];
            SpawnBlast(new Vector2(impactX, GroundY));
            Impact(impactX);
        }
    }

    private void SplitMeteor(Vector2 position, float speed)
    {
        if (MeteorCount >= MaxMeteors)
        {
            return;
        }

        meteors[MeteorCount++] = new Meteor
        {
            Start = position,
            Position = position,
            Direction = Vector2.Normalize(PickTarget() - position),
            Speed = speed,
            CanSplit = false,
            SplitY = 0f,
        };
    }

    private void SpawnBlast(Vector2 center)
    {
        if (BlastSpawnCount < blastSpawnPositions.Length)
        {
            blastSpawnPositions[BlastSpawnCount++] = center;
        }

        if (BlastCount >= MaxBlasts)
        {
            return;
        }

        blasts[BlastCount++] = new Blast
        {
            Center = center,
            Radius = BlastStartRadius,
            Growing = true,
            Hold = 0f,
        };
    }

    private void Impact(float x)
    {
        for (var cityIndex = 0; cityIndex < CityCount; cityIndex++)
        {
            if (!cities[cityIndex] || MathF.Abs(CityX[cityIndex] - x) > CityHalfWidth)
            {
                continue;
            }

            cities[cityIndex] = false;
            CitiesLeft--;
            CityLostThisFrame = cityIndex;
        }

        if (CitiesLeft == 0)
        {
            GameOver = true;
        }
    }

    private float Chance() => (float)random.NextDouble();
}
