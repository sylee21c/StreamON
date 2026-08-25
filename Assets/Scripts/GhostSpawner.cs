using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GhostSpawner : MonoBehaviour
{
    public enum AssaultState { Inactive, Combat, ClearingRemaining, Maintenance, GameOver }

    public readonly struct GhostDefeatInfo
    {
        public readonly int TierIndex;
        public readonly int BaseScore;
        public readonly float LifetimeSeconds;
        public readonly int Assault;
        public GhostDefeatInfo(int tierIndex, int baseScore, float lifetimeSeconds, int assault)
        { TierIndex = tierIndex; BaseScore = baseScore; LifetimeSeconds = lifetimeSeconds; Assault = assault; }
    }

    public static event Action<int> GhostDefeated;
    public static event Action<GhostDefeatInfo> GhostDefeatedDetailed;
    public event Action<AssaultState, int> StateChanged;

    [Serializable]
    public class GhostTier
    {
        public string tierName = "Tier 1";
        public GameObject[] prefabs;
        [Min(1f)] public float health = 100f;
        [Min(0f)] public float attackDamage = 10f;
        [Min(0f)] public float spawnWeight = 3f;
        public GameObject baseCoinPrefab;
        [Min(0)] public int baseCoinValue = 100;
        public GameObject bonusCoinPrefab;
        [Min(0)] public int bonusCoinValue = 500;
        [Range(0f, 1f)] public float bonusCoinChance = 0.2f;
        [Tooltip("리더보드용 기본 점수")]
        [Min(0)] public int baseScore = 25;
        [Tooltip("일반 가중치 풀에 들어오기 시작하는 공세")]
        [Min(1)] public int unlockAssault = 1;
    }

    [Serializable]
    public class NightProfile
    {
        public string label = "Assault";
        [Tooltip("기존 데이터 호환용. 공세 난이도 저작 힌트로만 유지됩니다.")]
        [Min(1)] public int totalGhosts = 10;
        [Min(0.05f)] public float spawnInterval = 5f;
        [Min(0f)] public float tier1Weight = 6f;
        [Min(0f)] public float tier2Weight = 3f;
        [Min(0f)] public float tier3Weight = 1f;
    }

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Endless Night Timing")]
    [SerializeField, Min(1f)] private float baseCombatDuration = 45f;
    [SerializeField, Min(0f)] private float combatDurationPerAssault = 2f;
    [SerializeField, Min(1f)] private float maintenanceDuration = 12f;
    [SerializeField, Min(0)] private int earlyMaintenanceCoinReward;

    [Header("Spawn Scaling")]
    [SerializeField, Min(0.05f)] private float baseSpawnInterval = 4f;
    [SerializeField, Min(0.05f)] private float minimumSpawnInterval = 0.45f;
    [SerializeField, Min(0f)] private float spawnIntervalReductionPerAssault = 0.12f;
    [SerializeField, Min(1)] private int baseSpawnBurst = 1;
    [SerializeField, Min(0f)] private float spawnBurstIncreasePerAssault = 0.2f;
    [SerializeField, Min(1)] private int baseMaxGhostsAlive = 30;
    [SerializeField, Min(0)] private int maxGhostsAliveIncreasePerAssault = 2;

    [Header("Stat Difficulty Scaling")]
    [SerializeField, Min(0f)] private float healthMultiplierPerAssault = 0.08f;
    [SerializeField, Min(0f)] private float attackMultiplierPerAssault = 0.05f;
    [SerializeField, Min(0f)] private float moveSpeedMultiplierPerAssault = 0.015f;

    [Header("Elite / Boss Assaults")]
    [SerializeField, Min(0)] private int eliteTierIndex = 1;
    [SerializeField, Min(1)] private int eliteUnlockAssault = 3;
    [SerializeField, Range(0f, 1f)] private float eliteChanceAtUnlock = 0.08f;
    [SerializeField, Range(0f, 1f)] private float eliteChanceIncreasePerAssault = 0.025f;
    [SerializeField, Min(0)] private int bossTierIndex = 2;
    [SerializeField, Min(0)] private int bossFirstAssault = 5;
    [SerializeField, Min(0)] private int bossAssaultInterval = 5;
    [SerializeField, Min(1)] private int bossCountPerAssault = 1;

    [Header("Wave Profiles (legacy Night Profiles, reused)")]
    [SerializeField] private NightProfile[] nightProfiles = Array.Empty<NightProfile>();
    [Header("Ghost Tiers")]
    [SerializeField] private GhostTier[] tiers = Array.Empty<GhostTier>();

    [Header("Ghost AI Stats")]
    [SerializeField, Min(0f)] private float ghostMoveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float ghostAttackRange = 1.2f;
    [SerializeField, Min(0f)] private float ghostAttackDamage = 10f;
    [SerializeField, Min(0f)] private float ghostAttackCooldown = 1.5f;
    [SerializeField, Min(0f)] private float ghostAggroRange = 5f;
    [SerializeField, Min(0f)] private float ghostDeaggroRange = 8f;
    [SerializeField] private float ghostFlightHeight = 0.25f;

    [Header("Ghost Health Bar")]
    [SerializeField] private Vector3 ghostHealthBarOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector2 ghostHealthBarPixelSize = new Vector2(60f, 7f);
    [SerializeField, Min(0.0001f)] private float ghostHealthBarWorldScale = 0.007f;

    private readonly List<GameObject> activeGhosts = new List<GameObject>();
    private float stateTimer;
    private float spawnTimer;
    private int bossesSpawnedThisAssault;

    public AssaultState CurrentState { get; private set; } = AssaultState.Inactive;
    public int CurrentAssault { get; private set; }
    public float StateSecondsRemaining => Mathf.Max(0f, stateTimer);
    public int ActiveGhostCount { get { PruneGhosts(); return activeGhosts.Count; } }
    public bool CanEndMaintenanceEarly => CurrentState == AssaultState.Maintenance;

    private void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            GameObject parent = GameObject.Find("GhostSpawn");
            if (parent != null)
            {
                spawnPoints = new Transform[parent.transform.childCount];
                for (int i = 0; i < parent.transform.childCount; i++) spawnPoints[i] = parent.transform.GetChild(i);
            }
        }
    }

    private void Update()
    {
        bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Night;
        if (CurrentState == AssaultState.Inactive || CurrentState == AssaultState.GameOver) return;
        if (!isNight && CurrentState != AssaultState.Maintenance) return;

        PruneGhosts();
        if (CurrentState == AssaultState.Combat)
        {
            stateTimer -= Time.deltaTime;
            spawnTimer -= Time.deltaTime;
            if (stateTimer <= 0f) { SetState(AssaultState.ClearingRemaining); return; }
            int maxAlive = baseMaxGhostsAlive + Mathf.Max(0, CurrentAssault - 1) * maxGhostsAliveIncreasePerAssault;
            if (spawnTimer <= 0f && activeGhosts.Count < maxAlive)
            {
                int burst = Mathf.Max(1, baseSpawnBurst + Mathf.FloorToInt((CurrentAssault - 1) * spawnBurstIncreasePerAssault));
                for (int i = 0; i < burst && activeGhosts.Count < maxAlive; i++) SpawnGhost();
                spawnTimer = CurrentSpawnInterval();
            }
        }
        else if (CurrentState == AssaultState.ClearingRemaining)
        {
            if (activeGhosts.Count == 0) BeginMaintenance();
        }
        else if (CurrentState == AssaultState.Maintenance)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) BeginNextAssault();
        }
    }

    public void StartSpawning() { ClearGhosts(); CurrentAssault = 0; BeginNextAssault(); }
    public void EndMaintenanceEarly()
    {
        if (CurrentState != AssaultState.Maintenance) return;
        if (earlyMaintenanceCoinReward > 0) CoinWallet.Instance?.Add(earlyMaintenanceCoinReward);
        BeginNextAssault();
    }
    public void StopSpawning() { ClearGhosts(); CurrentState = AssaultState.Inactive; stateTimer = 0f; }
    public void NotifyGameOver()
    {
        CurrentState = AssaultState.GameOver;
        stateTimer = 0f;
        StateChanged?.Invoke(CurrentState, CurrentAssault);
    }

    private void BeginNextAssault()
    {
        CurrentAssault++;
        bossesSpawnedThisAssault = 0;
        stateTimer = Mathf.Max(1f, baseCombatDuration + (CurrentAssault - 1) * combatDurationPerAssault);
        spawnTimer = 0f;
        DayNightManager.Instance?.BeginNextNight();
        SetState(AssaultState.Combat);
    }
    private void BeginMaintenance()
    {
        stateTimer = Mathf.Max(1f, maintenanceDuration);
        DayNightManager.Instance?.BeginMaintenanceDay();
        SetState(AssaultState.Maintenance);
    }
    private void SetState(AssaultState state) { CurrentState = state; StateChanged?.Invoke(state, CurrentAssault); }

    private float CurrentSpawnInterval()
    {
        NightProfile profile = GetCurrentProfile();
        float authoredBase = profile != null && profile.spawnInterval > 0f ? profile.spawnInterval : baseSpawnInterval;
        return Mathf.Max(minimumSpawnInterval, authoredBase - (CurrentAssault - 1) * spawnIntervalReductionPerAssault);
    }
    private NightProfile GetCurrentProfile()
    {
        if (nightProfiles == null || nightProfiles.Length == 0) return null;
        return nightProfiles[Mathf.Clamp(CurrentAssault - 1, 0, nightProfiles.Length - 1)];
    }

    private void SpawnGhost()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        int tierIndex = PickTierIndex();
        if (tierIndex < 0 || tierIndex >= tiers.Length) return;
        GhostTier tier = tiers[tierIndex];
        if (tier == null || tier.prefabs == null || tier.prefabs.Length == 0) return;
        GameObject prefab = tier.prefabs[UnityEngine.Random.Range(0, tier.prefabs.Length)];
        if (prefab == null) return;
        Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        GameObject ghost = Instantiate(prefab, point.position, point.rotation);
        float difficultyIndex = Mathf.Max(0, CurrentAssault - 1);
        Damageable health = ghost.GetComponent<Damageable>() ?? ghost.AddComponent<Damageable>();
        health.SetMaxHealth(tier.health * (1f + difficultyIndex * healthMultiplierPerAssault));
        float spawnedAt = Time.time;
        int assaultAtSpawn = CurrentAssault;
        health.OnDeath += () =>
        {
            float lifetime = Mathf.Max(0f, Time.time - spawnedAt);
            GhostDefeated?.Invoke(tierIndex);
            GhostDefeatedDetailed?.Invoke(new GhostDefeatInfo(tierIndex, tier.baseScore, lifetime, assaultAtSpawn));
        };

        GhostAI ai = ghost.GetComponent<GhostAI>() ?? ghost.AddComponent<GhostAI>();
        float baseDamage = tier.attackDamage > 0f ? tier.attackDamage : ghostAttackDamage;
        ai.ConfigureStats(ghostMoveSpeed * (1f + difficultyIndex * moveSpeedMultiplierPerAssault), ghostAttackRange,
            baseDamage * (1f + difficultyIndex * attackMultiplierPerAssault), ghostAttackCooldown,
            ghostAggroRange, ghostDeaggroRange, ghostFlightHeight);
        ai.SetCoinDrops(tier.baseCoinPrefab, tier.baseCoinValue, tier.bonusCoinPrefab, tier.bonusCoinValue, tier.bonusCoinChance);
        if (ghost.GetComponent<HealthBar>() == null)
            ghost.AddComponent<HealthBar>().Configure(ghostHealthBarOffset, ghostHealthBarPixelSize, ghostHealthBarWorldScale);
        activeGhosts.Add(ghost);
    }

    private int PickTierIndex()
    {
        bool bossAssault = bossFirstAssault > 0 && CurrentAssault >= bossFirstAssault && bossAssaultInterval > 0
            && (CurrentAssault - bossFirstAssault) % bossAssaultInterval == 0;
        if (bossAssault && bossesSpawnedThisAssault < bossCountPerAssault && IsTierUsable(bossTierIndex))
        { bossesSpawnedThisAssault++; return bossTierIndex; }
        if (CurrentAssault >= eliteUnlockAssault && IsTierUsable(eliteTierIndex))
        {
            float chance = eliteChanceAtUnlock + (CurrentAssault - eliteUnlockAssault) * eliteChanceIncreasePerAssault;
            if (UnityEngine.Random.value < Mathf.Clamp01(chance)) return eliteTierIndex;
        }

        NightProfile profile = GetCurrentProfile();
        float total = 0f;
        for (int i = 0; i < tiers.Length; i++) total += TierWeight(i, profile);
        if (total <= 0f) return IsTierUsable(0) ? 0 : -1;
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < tiers.Length; i++) { roll -= TierWeight(i, profile); if (roll <= 0f) return i; }
        return tiers.Length - 1;
    }
    private float TierWeight(int index, NightProfile profile)
    {
        if (!IsTierUsable(index)) return 0f;
        if (profile != null)
        {
            if (index == 0) return Mathf.Max(0f, profile.tier1Weight);
            if (index == 1) return Mathf.Max(0f, profile.tier2Weight);
            if (index == 2) return Mathf.Max(0f, profile.tier3Weight);
        }
        return Mathf.Max(0f, tiers[index].spawnWeight);
    }
    private bool IsTierUsable(int index) => tiers != null && index >= 0 && index < tiers.Length && tiers[index] != null
        && CurrentAssault >= Mathf.Max(1, tiers[index].unlockAssault) && tiers[index].prefabs != null && tiers[index].prefabs.Length > 0;
    private void PruneGhosts() => activeGhosts.RemoveAll(ghost => ghost == null);
    private void ClearGhosts() { foreach (GameObject ghost in activeGhosts) if (ghost != null) Destroy(ghost); activeGhosts.Clear(); }
}
