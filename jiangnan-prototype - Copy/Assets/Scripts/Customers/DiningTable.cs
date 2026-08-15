using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TableStatusType
{
    HasEmptySeats,
    Full,
    CollectPayment,
    Broken
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public class DiningTable : MonoBehaviour
{
    private const int MinLevel = 1;
    private const int MaxLevel = 3;

    [SerializeField] private TableSeat[] _seats;
    [SerializeField] private Transform _statusPoint;
    [SerializeField] private int _level;
    [SerializeField] private int _upgradeToLevel2Cost;
    [SerializeField] private int _upgradeToLevel3Cost;
    [SerializeField] private Collider _tapCollider;
    [SerializeField] private Transform _paymentAnchor;
    [SerializeField] private Transform _deliveryTarget;
    [SerializeField] private Transform _vfxPoint;
    [Tooltip("VIP / second-floor table. Only VIP customers may sit here.")]
    [SerializeField] private bool _isVipTable;
    [Header("Broken State")]
    [Tooltip("Gold required to repair this table and its stools after a prankster breaks them.")]
    [SerializeField] private int _repairCost = 500;
    [Header("Level Visuals — drag Table + Seats GameObjects for each level")]
    [SerializeField] private TableLevelVisualSet _level1Visuals;
    [SerializeField] private TableLevelVisualSet _level2Visuals;
    [SerializeField] private TableLevelVisualSet _level3Visuals;

    private bool _isUpgrading;
    private bool _isRepairing;
    private bool _isBroken;
    private GameObject _activeBrokenTable;
    private GameObject _level2EquipmentTable;
    private GameObject _level3EquipmentTable;
    private Vector3 _level2AuthoredTableScale = Vector3.one;
    private Vector3 _level3AuthoredTableScale = Vector3.one;
    private bool _authoredTableScalesCached;

    private bool _saveIndexResolved;
    private int _cachedSaveIndex = -1;

    public int Level => _level;
    public bool IsBroken => _isBroken;
    public bool IsRepairing => _isRepairing;
    public int RepairCost => _repairCost;
    public bool IsVipTable => _isVipTable;
    public RestaurantFloor Floor => _isVipTable
        ? RestaurantFloor.Second
        : RestaurantFloorUtil.ResolveFloor(transform);
    public bool CanBeBrokenByPrankster => !_isVipTable && !_isBroken && !_isUpgrading && !_isRepairing;
    public int MaxTableLevel => MaxLevel;
    public bool CanUpgrade => !_isVipTable && _level < MaxLevel && !_isUpgrading && !_isBroken && !_isRepairing;
    public bool IsUpgrading => _isUpgrading;
    public Transform StatusPoint => _statusPoint != null ? _statusPoint : transform;
    public Transform PaymentAnchor => GetPaymentAnchor();

    public bool ContainsSeat(TableSeat seat)
    {
        return seat != null && seat.transform.IsChildOf(transform);
    }

    private void Awake()
    {
        if (_statusPoint == null)
        {
            Transform found = transform.Find("Status Point");

            if (found != null)
                _statusPoint = found;
        }

        if (_paymentAnchor == null)
        {
            Transform collectPoint = transform.Find("Collect Point");

            if (collectPoint != null)
                _paymentAnchor = collectPoint;
        }

        if (_vfxPoint == null)
        {
            Transform foundVfxPoint = transform.Find("VFX Point");

            if (foundVfxPoint != null)
                _vfxPoint = foundVfxPoint;
        }

        EnsureTapCollider();

        // VIP tables are static (no upgrades / broken variants / save index).
        if (_isVipTable)
            return;

        CacheAuthoredTableScales();
        ApplySavedTableLevel();
        EnsureBrokenTableReferences();
    }

    private void OnEnable()
    {
        if (_isBroken || _isVipTable)
            return;

        RefreshVisualsForBuild();
    }

    private void Start()
    {
        if (!_isBroken && !_isVipTable)
            RefreshVisualsForBuild();

        if (!_isVipTable)
            ApplySavedBrokenState();

        RegisterSeatsWithCustomerManager();
        GameEvents.RaiseTableStatusChanged(this);
    }

    /// <summary>
    /// Ensures the correct level mesh/seats are visible after the table root is activated
    /// by a build spot (including mid-session builds that happen after scene Start).
    /// </summary>
    public void RefreshVisualsForBuild()
    {
        if (_isBroken || _isVipTable)
            return;

        RestoreVisualLevel(_level);
        RefreshRuntimeReferences();
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void OnDestroy()
    {
        UnregisterSeatsWithCustomerManager();
        HideBrokenVisual();
    }

    public void NotifyClicked()
    {
        if (_isUpgrading)
            return;

        GameEvents.RaiseTableClicked(this);
    }

    public bool HasVipOccupant()
    {
        if (_seats == null)
            return false;

        for (int i = 0; i < _seats.Length; i++)
        {
            TableSeat seat = _seats[i];

            if (seat != null && seat.IsOccupied && seat.Occupant != null && seat.Occupant.IsVip)
                return true;
        }

        return false;
    }

    public Vector3 GetPranksterApproachPosition()
    {
        return GetDeliveryTargetPosition();
    }

    public void BreakByPrankster()
    {
        if (_isBroken)
            return;

        SetBroken(true);
        PlayTableSfx(SfxId.PranksterBreakTable);
        UIManager.Instance?.PlayTableBreakDustEffect(this);
    }

    public bool CanRepair()
    {
        if (!_isBroken || _isRepairing || _isUpgrading || GoldManager.Instance == null)
            return false;

        if (TableUpgradeDelivery.Instance != null && TableUpgradeDelivery.Instance.IsDelivering)
            return false;

        return true;
    }

    public bool TryRepair()
    {
        if (!CanRepair())
            return false;

        if (!GoldManager.Instance.TrySpend(_repairCost))
            return false;

        StartCoroutine(RepairRoutine());
        return true;
    }

    private IEnumerator RepairRoutine()
    {
        _isRepairing = true;
        GameEvents.RaiseTableStatusChanged(this);

        if (TableUpgradeDelivery.Instance != null)
            yield return TableUpgradeDelivery.Instance.DeliverRepair(GetDeliveryTargetPosition());

        CompleteRepair();
        _isRepairing = false;
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void CompleteRepair()
    {
        RestoreFromBroken();
        ResetToLevelOneAfterRepair();
        PlayTableCompletionVfx();
        PlayTableSfx(SfxId.TableRepair);
    }

    private void PlayTableCompletionVfx()
    {
        Transform vfxPoint = GetVfxPoint();

        if (vfxPoint == null)
            return;

        TableUpgradeDelivery.Instance?.PlayCompletionVfx(vfxPoint);
    }

    private Transform GetVfxPoint()
    {
        if (_vfxPoint != null)
            return _vfxPoint;

        return BuildCompletionVfx.ResolveVfxPoint(null, gameObject);
    }

    public AudioSource GetTableAudioSource()
    {
        return GetComponent<AudioSource>();
    }

    private void PlayTableSfx(SfxId sfx)
    {
        AudioManager.PlayOn(GetTableAudioSource(), sfx);
    }

    private void ResetToLevelOneAfterRepair()
    {
        if (_level <= MinLevel)
            return;

        TableLevelVisualSet previousVisuals = GetVisualSet(_level);
        TableSeat[] previousSeats = CaptureSeats(previousVisuals);

        for (int i = MinLevel; i <= MaxLevel; i++)
            SetVisualSetActive(GetVisualSet(i), false);

        _level = MinLevel;
        PlayerProfileStorage.SetTableLevelForCurrentPlayer(ResolveSaveIndex(), MinLevel);
        SetVisualSetActive(GetVisualSet(MinLevel), true);
        RefreshRuntimeReferences();
        UpdateSeatRegistry(previousSeats);
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void SetBroken(bool broken)
    {
        _isBroken = broken;

        if (broken)
            ApplyBrokenVisual();
        else
            HideBrokenVisual();

        PersistBrokenState();
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void RestoreFromBroken()
    {
        if (!_isBroken)
            return;

        _isBroken = false;
        HideBrokenVisual();
        SetVisualSetActive(GetVisualSet(_level), true);
        RefreshRuntimeReferences();

        if (_tapCollider != null)
            _tapCollider.enabled = true;

        PersistBrokenState();
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void ApplySavedBrokenState()
    {
        int saveIndex = ResolveSaveIndex();

        if (saveIndex < 0 || !PlayerProfileStorage.IsTableBrokenForCurrentPlayer(saveIndex))
            return;

        _isBroken = true;
        ApplyBrokenVisual();
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void PersistBrokenState()
    {
        int saveIndex = ResolveSaveIndex();

        if (saveIndex < 0)
            return;

        PlayerProfileStorage.SetTableBrokenForCurrentPlayer(saveIndex, _isBroken);
    }

    private void ApplyBrokenVisual()
    {
        TableLevelVisualSet currentVisuals = GetVisualSet(_level);

        if (currentVisuals == null)
            return;

        GameObject sourceTable = ResolveTableVisual(currentVisuals, _level, allowEquipmentClone: IsEquipmentCloneLevel(_level));
        Transform snapAnchor = sourceTable != null ? sourceTable.transform : GetTableTransform(currentVisuals, _level);

        HideAllLevelVisuals();
        HideAllBrokenVisuals();

        GameObject brokenTable = currentVisuals.BrokenTable;

        if (brokenTable == null)
            return;

        if (snapAnchor != null)
            SnapToAnchor(brokenTable.transform, snapAnchor);

        SetActiveInHierarchy(brokenTable, true);
        _activeBrokenTable = brokenTable;
        RefreshBrokenTapCollider(brokenTable);
    }

    private void HideBrokenVisual()
    {
        if (_activeBrokenTable == null)
            return;

        _activeBrokenTable.SetActive(false);
        _activeBrokenTable = null;
    }

    private void HideAllBrokenVisuals()
    {
        DeactivateBrokenTable(_level1Visuals);
        DeactivateBrokenTable(_level2Visuals);
        DeactivateBrokenTable(_level3Visuals);
    }

    private static void DeactivateBrokenTable(TableLevelVisualSet visuals)
    {
        if (visuals?.BrokenTable != null)
            visuals.BrokenTable.SetActive(false);
    }

    private void RefreshBrokenTapCollider(GameObject brokenTable)
    {
        if (brokenTable == null)
            return;

        Collider collider = brokenTable.GetComponentInChildren<Collider>();

        if (collider != null)
            _tapCollider = collider;
    }

    private void EnsureBrokenTableReferences()
    {
        if (!TryGetTableDisplayNumber(out int tableNumber))
            return;

        string suffix = $" ({tableNumber})";
        AssignBrokenReferenceIfMissing(_level1Visuals, $"TableLv1_Broken{suffix}");
        AssignBrokenReferenceIfMissing(_level2Visuals, $"TableLv2_Broken{suffix}");
        AssignBrokenReferenceIfMissing(_level3Visuals, $"TableLv3_Broken{suffix}");
        HideAllBrokenVisuals();
    }

    private bool TryGetTableDisplayNumber(out int tableNumber)
    {
        tableNumber = -1;

        if (!TryParseTableIndexFromName(name, out int tableIndex))
            return false;

        tableNumber = tableIndex + 1;
        return tableNumber > 0;
    }

    private static void AssignBrokenReferenceIfMissing(TableLevelVisualSet visuals, string objectName)
    {
        if (visuals == null || visuals.BrokenTable != null)
            return;

        GameObject found = FindReferenceTable(objectName);

        if (found != null)
            visuals.BrokenTable = found;
    }

    private static GameObject FindReferenceTable(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform referenceRoot = null;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == "ReferenceTables")
            {
                referenceRoot = candidate;
                break;
            }
        }

        if (referenceRoot != null)
        {
            Transform found = referenceRoot.Find(objectName);
            if (found != null)
                return found.gameObject;

            for (int i = 0; i < referenceRoot.childCount; i++)
            {
                Transform child = referenceRoot.GetChild(i);
                if (child != null && child.name == objectName)
                    return child.gameObject;
            }
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
                return candidate.gameObject;
        }

        return null;
    }

    private static void SetActiveInHierarchy(GameObject target, bool active)
    {
        if (target == null)
            return;

        if (!active)
        {
            target.SetActive(false);
            return;
        }

        // Activate parents first so off-screen reference templates can become visible when used.
        Transform current = target.transform;
        List<Transform> chain = new();
        while (current != null)
        {
            chain.Add(current);
            current = current.parent;
        }

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i] != null && !chain[i].gameObject.activeSelf)
                chain[i].gameObject.SetActive(true);
        }
    }

    private void HideAllLevelVisuals()
    {
        for (int i = MinLevel; i <= MaxLevel; i++)
            SetVisualSetActive(GetVisualSet(i), false);
    }

    public bool TryBeginUpgrade()
    {
        if (!CanUpgrade)
            return false;

        if (TableUpgradeDelivery.Instance != null && TableUpgradeDelivery.Instance.IsDelivering)
            return false;

        int cost = GetUpgradeCost();

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(cost))
            return false;

        StartCoroutine(UpgradeRoutine());
        return true;
    }

    public int GetUpgradeCost()
    {
        return _level switch
        {
            1 => _upgradeToLevel2Cost,
            2 => _upgradeToLevel3Cost,
            _ => 0
        };
    }

    public TableStatusType GetCurrentStatus()
    {
        if (_isBroken)
            return TableStatusType.Broken;

        EnsureRuntimeSeatsReady();

        if (HasAwaitingPayment())
            return TableStatusType.CollectPayment;

        if (HasEmptySeat())
            return TableStatusType.HasEmptySeats;

        return TableStatusType.Full;
    }

    private void EnsureRuntimeSeatsReady()
    {
        if (HasValidRuntimeSeats())
            return;

        // VIP seats are inspector-authored on the table itself — never rebuild from level visuals.
        if (_isVipTable)
            return;

        RefreshRuntimeReferences();
    }

    private bool HasValidRuntimeSeats()
    {
        if (_seats == null || _seats.Length == 0)
            return false;

        for (int i = 0; i < _seats.Length; i++)
        {
            TableSeat seat = _seats[i];

            if (seat == null || !seat.isActiveAndEnabled)
                return false;
        }

        return true;
    }

    private IEnumerator UpgradeRoutine()
    {
        _isUpgrading = true;
        GameEvents.RaiseTableStatusChanged(this);
        int nextLevel = _level + 1;

        if (TableUpgradeDelivery.Instance != null)
            yield return TableUpgradeDelivery.Instance.DeliverUpgrade(nextLevel, GetDeliveryTargetPosition());

        ApplyLevel(nextLevel);
        PlayTableCompletionVfx();
        PlayTableSfx(SfxId.TableUpgrade);
        _isUpgrading = false;
        GameEvents.RaiseTableUpgraded(this);
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void ApplyLevel(int level)
    {
        int previousLevel = _level;
        _level = Mathf.Clamp(level, MinLevel, MaxLevel);

        if (previousLevel == _level)
            return;

        PlayerProfileStorage.SetTableLevelForCurrentPlayer(ResolveSaveIndex(), _level);

        TableLevelVisualSet previousVisuals = GetVisualSet(previousLevel);
        TableLevelVisualSet nextVisuals = GetVisualSet(_level);

        if (nextVisuals == null)
        {
            Debug.LogWarning($"DiningTable on {name} is missing level {_level} visuals.", this);
            return;
        }

        TableSeat[] previousSeats = CaptureSeats(previousVisuals);

        SetVisualSetActive(previousVisuals, false);

        SnapVisualSetToPreviousLevel(_level);

        SetVisualSetActive(nextVisuals, true);
        MigrateOccupants(previousSeats, CaptureSeats(nextVisuals, includeInactive: true));
        RefreshRuntimeReferences();
        UpdateSeatRegistry(previousSeats);
        GameEvents.RaiseTableStatusChanged(this);
    }

    private void ShowOnlyLevel(int level)
    {
        for (int i = MinLevel; i <= MaxLevel; i++)
            SetVisualSetActive(GetVisualSet(i), i == level);
    }

    private void RestoreVisualLevel(int targetLevel)
    {
        targetLevel = Mathf.Clamp(targetLevel, MinLevel, MaxLevel);

        if (targetLevel == MinLevel)
        {
            ShowOnlyLevel(MinLevel);
            return;
        }

        for (int i = MinLevel; i <= MaxLevel; i++)
            SetVisualSetActive(GetVisualSet(i), false);

        for (int level = MinLevel + 1; level <= targetLevel; level++)
            SnapVisualSetToPreviousLevel(level);

        SetVisualSetActive(GetVisualSet(targetLevel), true);
    }

    private void SnapVisualSetToPreviousLevel(int level)
    {
        int previousLevel = level - 1;
        TableLevelVisualSet previousVisuals = GetVisualSet(previousLevel);
        TableLevelVisualSet nextVisuals = GetVisualSet(level);

        if (nextVisuals == null)
            return;

        Transform tableAnchor = GetTableTransform(previousVisuals, previousLevel);
        Transform seatsAnchor = GetSeatsTransform(previousVisuals);
        // Level 2/3 install the carrier equipment clone — snap that instance, not the hidden authored ref.
        GameObject nextTable = ResolveTableVisual(
            nextVisuals,
            level,
            allowEquipmentClone: IsEquipmentCloneLevel(level));
        GameObject nextSeats = nextVisuals.Seats;

        if (nextTable != null && tableAnchor != null)
        {
            SnapToAnchor(
                nextTable.transform,
                tableAnchor,
                GetAuthoredTableScale(level, nextTable));
        }

        if (nextSeats != null && seatsAnchor != null)
            SnapToAnchor(nextSeats.transform, seatsAnchor);
    }

    private void SetVisualSetActive(TableLevelVisualSet visuals, bool active)
    {
        if (visuals == null)
            return;

        int visualLevel = visuals == _level1Visuals ? 1 : visuals == _level2Visuals ? 2 : visuals == _level3Visuals ? 3 : _level;
        bool allowEquipmentClone = active && IsEquipmentCloneLevel(visualLevel);
        GameObject table = ResolveTableVisual(visuals, visualLevel, allowEquipmentClone);

        if (!active)
        {
            if (visualLevel == 2 && _level2EquipmentTable != null)
                _level2EquipmentTable.SetActive(false);

            if (visualLevel == 3 && _level3EquipmentTable != null)
                _level3EquipmentTable.SetActive(false);
        }

        if (table != null)
        {
            if (active)
                SetActiveInHierarchy(table, true);
            else
                table.SetActive(false);

            if (active)
                RuntimeMeshVisibility.Prepare(table.transform);
        }

        if (visuals.Seats != null)
        {
            if (active)
                SetActiveInHierarchy(visuals.Seats, true);
            else
                visuals.Seats.SetActive(false);

            if (active)
                RuntimeMeshVisibility.Prepare(visuals.Seats.transform);
        }
    }

    private static void SnapToAnchor(Transform target, Transform anchor)
    {
        if (target == null)
            return;

        SnapToAnchor(target, anchor, target.localScale);
    }

    private static void SnapToAnchor(Transform target, Transform anchor, Vector3 localScale)
    {
        if (target == null || anchor == null)
            return;

        RuntimeMeshVisibility.Prepare(target);

        Transform parent = anchor.parent;
        target.SetParent(parent, false);
        target.localPosition = anchor.localPosition;
        target.localRotation = anchor.localRotation;
        target.localScale = localScale;
        target.SetSiblingIndex(anchor.GetSiblingIndex());

        RuntimeMeshVisibility.Prepare(target);
    }

    private void MigrateOccupants(TableSeat[] previousSeats, TableSeat[] nextSeats)
    {
        ClearSeatOccupants(nextSeats);

        if (previousSeats == null || nextSeats == null)
            return;

        int count = Mathf.Min(previousSeats.Length, nextSeats.Length);

        for (int i = 0; i < count; i++)
        {
            TableSeat oldSeat = previousSeats[i];
            TableSeat newSeat = nextSeats[i];

            if (oldSeat == null || newSeat == null)
                continue;

            Customer occupant = oldSeat.DetachOccupantForReplacement();

            if (occupant != null)
            {
                newSeat.RestoreOccupant(occupant);
                CustomerManager.Instance?.RebindAwaitingPaymentSeatIfNeeded(occupant, newSeat);
            }
        }
    }

    private static void ClearSeatOccupants(TableSeat[] seats)
    {
        if (seats == null)
            return;

        for (int i = 0; i < seats.Length; i++)
            seats[i]?.DetachOccupantForReplacement();
    }

    private void RefreshRuntimeReferences()
    {
        TableLevelVisualSet currentVisuals = GetVisualSet(_level);

        if (currentVisuals == null)
            return;

        GameObject tableObject = ResolveTableVisual(
            currentVisuals,
            _level,
            allowEquipmentClone: IsEquipmentCloneLevel(_level));

        if (tableObject != null)
        {
            Collider collider = tableObject.GetComponentInChildren<Collider>();

            if (collider != null)
                _tapCollider = collider;
        }

        _seats = FilterActiveSeats(CaptureSeats(currentVisuals, includeInactive: false));
        EnsureSeatComponents(_seats);
    }

    private static TableSeat[] FilterActiveSeats(TableSeat[] seats)
    {
        if (seats == null || seats.Length == 0)
            return Array.Empty<TableSeat>();

        int activeCount = 0;

        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null && seats[i].isActiveAndEnabled)
                activeCount++;
        }

        if (activeCount == 0)
            return Array.Empty<TableSeat>();

        TableSeat[] activeSeats = new TableSeat[activeCount];
        int writeIndex = 0;

        for (int i = 0; i < seats.Length; i++)
        {
            TableSeat seat = seats[i];

            if (seat != null && seat.isActiveAndEnabled)
                activeSeats[writeIndex++] = seat;
        }

        return activeSeats;
    }

    private void EnsureSeatComponents(TableSeat[] seats)
    {
        if (seats == null)
            return;

        Transform paymentAnchor = _paymentAnchor != null ? _paymentAnchor : GetPaymentAnchor();

        for (int i = 0; i < seats.Length; i++)
        {
            TableSeat seat = seats[i];

            if (seat == null)
                continue;

            seat.EnsureMissingAnchors(seat.transform, paymentAnchor);
        }
    }

    private TableSeat[] CaptureSeats(TableLevelVisualSet visuals, bool includeInactive = true)
    {
        Transform seatsRoot = visuals?.Seats != null ? visuals.Seats.transform : null;

        if (seatsRoot == null)
        {
            // Keep inspector-authored seats when present (VIP tables, hand-wired setups).
            if (HasAnySeatReference(_seats))
                return _seats;

            // Competitor / incomplete setups: discover a Seats child under the table.
            seatsRoot = FindChildSeatsRoot();
            if (seatsRoot == null)
                return _seats ?? Array.Empty<TableSeat>();
        }

        TableSeat[] existingSeats = seatsRoot.GetComponentsInChildren<TableSeat>(includeInactive);

        if (existingSeats != null && existingSeats.Length > 0)
            return existingSeats;

        int childCount = seatsRoot.childCount;

        if (childCount == 0)
            return Array.Empty<TableSeat>();

        TableSeat[] seats = new TableSeat[childCount];
        int seatCount = 0;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = seatsRoot.GetChild(i);

            if (!includeInactive && !child.gameObject.activeInHierarchy)
                continue;

            TableSeat seat = child.GetComponent<TableSeat>();

            if (seat == null)
                seat = child.gameObject.AddComponent<TableSeat>();

            seats[seatCount++] = seat;
        }

        if (seatCount == 0)
            return Array.Empty<TableSeat>();

        if (seatCount == seats.Length)
            return seats;

        TableSeat[] trimmedSeats = new TableSeat[seatCount];
        Array.Copy(seats, trimmedSeats, seatCount);
        return trimmedSeats;
    }

    private static bool HasAnySeatReference(TableSeat[] seats)
    {
        if (seats == null || seats.Length == 0)
            return false;

        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null)
                return true;
        }

        return false;
    }

    private Transform FindChildSeatsRoot()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child != null
                && child.name.IndexOf("Seats", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return null;
    }

    private GameObject ResolveTableVisual(TableLevelVisualSet visuals, int level, bool allowEquipmentClone)
    {
        // Prefer an already-installed equipment mesh even when only resolving an anchor.
        if (level == 2 && _level2EquipmentTable != null)
            return _level2EquipmentTable;

        if (level == 3 && _level3EquipmentTable != null)
            return _level3EquipmentTable;

        GameObject table = GetTableObject(visuals, level);

        if (!allowEquipmentClone || !IsEquipmentCloneLevel(level) || table == null)
            return table;

        GameObject template = TableUpgradeDelivery.Instance != null
            ? TableUpgradeDelivery.Instance.GetEquipmentTableTemplate(level)
            : null;

        if (template == null)
            return table;

        // Hide the authored placeholder; the delivered equipment mesh is the real upgraded table.
        table.SetActive(false);

        GameObject installed = Instantiate(template);
        installed.name = $"{template.name}_Installed";
        // Template lives under an inactive carrier — Instantiate copies inactive state.
        installed.SetActive(true);
        installed.transform.localScale = GetAuthoredTableScale(level, template);
        RuntimeMeshVisibility.Prepare(installed.transform);

        if (level == 2)
            _level2EquipmentTable = installed;
        else
            _level3EquipmentTable = installed;

        return installed;
    }

    private static bool IsEquipmentCloneLevel(int level)
    {
        return level == 2 || level == 3;
    }

    private GameObject GetTableObject(TableLevelVisualSet visuals, int level)
    {
        if (visuals?.Table != null)
            return visuals.Table;

        if (level == MinLevel && _tapCollider != null)
            return _tapCollider.gameObject;

        return null;
    }

    private Transform GetTableTransform(TableLevelVisualSet visuals, int level)
    {
        GameObject tableObject = ResolveTableVisual(
            visuals,
            level,
            allowEquipmentClone: IsEquipmentCloneLevel(level) && _level == level);
        return tableObject != null ? tableObject.transform : null;
    }

    private static Transform GetSeatsTransform(TableLevelVisualSet visuals)
    {
        return visuals?.Seats != null ? visuals.Seats.transform : null;
    }

    private TableLevelVisualSet GetVisualSet(int level)
    {
        return level switch
        {
            1 => _level1Visuals,
            2 => _level2Visuals,
            3 => _level3Visuals,
            _ => null
        };
    }

    private void CacheAuthoredTableScales()
    {
        if (_authoredTableScalesCached)
            return;

        _authoredTableScalesCached = true;
        _level2AuthoredTableScale = ReadLocalScale(_level2Visuals?.Table);
        _level3AuthoredTableScale = ReadLocalScale(_level3Visuals?.Table);
    }

    private Vector3 GetAuthoredTableScale(int level, GameObject fallback)
    {
        CacheAuthoredTableScales();

        GameObject referenceTable = GetVisualSet(level)?.Table;

        if (referenceTable != null)
        {
            return level switch
            {
                2 => _level2AuthoredTableScale,
                3 => _level3AuthoredTableScale,
                _ => ReadLocalScale(referenceTable)
            };
        }

        return ReadLocalScale(fallback);
    }

    private static Vector3 ReadLocalScale(GameObject source)
    {
        return source != null ? source.transform.localScale : Vector3.one;
    }

    private Vector3 GetDeliveryTargetPosition()
    {
        if (_deliveryTarget != null)
            return _deliveryTarget.position;

        TableLevelVisualSet currentVisuals = GetVisualSet(_level);
        Transform tableTransform = GetTableTransform(currentVisuals, _level);

        if (tableTransform != null)
            return tableTransform.position;

        return transform.position;
    }

    private bool HasAwaitingPayment()
    {
        if (_isBroken)
            return false;

        Transform paymentAnchor = GetPaymentAnchor();

        if (paymentAnchor == null || CustomerManager.Instance == null)
            return false;

        return CustomerManager.Instance.HasAwaitingPaymentsAt(paymentAnchor);
    }

    private bool HasEmptySeat()
    {
        if (_isBroken)
            return false;

        EnsureRuntimeSeatsReady();

        if (_seats == null || _seats.Length == 0)
            return true;

        for (int i = 0; i < _seats.Length; i++)
        {
            TableSeat seat = _seats[i];

            if (seat != null && seat.isActiveAndEnabled && !seat.IsOccupied)
                return true;
        }

        return false;
    }

    private Transform GetPaymentAnchor()
    {
        if (_paymentAnchor != null)
            return _paymentAnchor;

        if (_seats == null)
            return null;

        for (int i = 0; i < _seats.Length; i++)
        {
            if (_seats[i] != null)
                return _seats[i].PaymentUiAnchor;
        }

        return null;
    }

    private void EnsureTapCollider()
    {
        if (_tapCollider == null)
            _tapCollider = GetComponentInChildren<Collider>();

        if (_tapCollider != null)
            return;

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.center = new Vector3(0f, 0.5f, 0f);
        boxCollider.size = new Vector3(1.2f, 1f, 1.2f);
        _tapCollider = boxCollider;
    }

    private void RegisterSeatsWithCustomerManager()
    {
        if (CustomerManager.Instance == null || _seats == null)
            return;

        CustomerManager.Instance.RegisterSeats(_seats);
    }

    private void UnregisterSeatsWithCustomerManager()
    {
        if (CustomerManager.Instance == null || _seats == null)
            return;

        CustomerManager.Instance.UnregisterSeats(_seats);
    }

    private void UpdateSeatRegistry(TableSeat[] previousSeats)
    {
        if (CustomerManager.Instance == null)
            return;

        if (previousSeats != null)
            CustomerManager.Instance.UnregisterSeats(previousSeats);

        if (_seats != null)
            CustomerManager.Instance.RegisterSeats(_seats);
    }

    private void ApplySavedTableLevel()
    {
        if (_isVipTable)
            return;

        int saveIndex = ResolveSaveIndex();

        if (saveIndex < 0)
            return;

        _level = PlayerProfileStorage.GetTableLevelForCurrentPlayer(saveIndex);
    }

    private int ResolveSaveIndex()
    {
        if (_isVipTable)
            return -1;

        if (_saveIndexResolved)
            return _cachedSaveIndex;

        _saveIndexResolved = true;

        if (TryParseTableIndexFromName(name, out int index))
        {
            _cachedSaveIndex = index;
            return index;
        }

        DiningTable[] tables = FindObjectsOfType<DiningTable>(true);
        System.Array.Sort(tables, (left, right) => string.CompareOrdinal(left.name, right.name));

        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i] == this)
            {
                _cachedSaveIndex = i;
                return i;
            }
        }

        return _cachedSaveIndex;
    }

    private static bool TryParseTableIndexFromName(string objectName, out int index)
    {
        index = -1;

        if (string.IsNullOrEmpty(objectName))
            return false;

        int openParenthesis = objectName.LastIndexOf('(');
        int closeParenthesis = objectName.LastIndexOf(')');

        if (openParenthesis < 0 || closeParenthesis <= openParenthesis)
            return false;

        string numberText = objectName.Substring(openParenthesis + 1, closeParenthesis - openParenthesis - 1);

        if (!int.TryParse(numberText, out int tableNumber) || tableNumber <= 0)
            return false;

        index = tableNumber - 1;
        return true;
    }
}
