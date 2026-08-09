using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    [Header("Base Table Payment")]
    [SerializeField] private int _tableLevel1Payment;
    [SerializeField] private int _tableLevel2Payment;
    [SerializeField] private int _tableLevel3Payment;

    [Header("Customer Payment")]
    [SerializeField] private int _normalCustomerPayment;
    [SerializeField] private int _vipPayment;

    private static int? _sessionGold;
    private static string _sessionPlayerName;

    private int _currentGold;
    private int _businessSessionIncome;
    private bool _trackBusinessSessionIncome;

    public int CurrentGold => _currentGold;
    public int BusinessSessionIncome => _businessSessionIncome;

    public int GetCustomerPaymentForTableLevel(int tableLevel)
    {
        return tableLevel switch
        {
            2 => _tableLevel2Payment,
            3 => _tableLevel3Payment,
            _ => _tableLevel1Payment
        };
    }

    public int GetCustomerPayment(Customer customer, int tableLevel)
    {
        int payment = GetCustomerPaymentForTableLevel(tableLevel);
        int levelMultiplier = Mathf.Clamp(tableLevel, 1, 3);

        if (customer != null && customer.IsVip)
        {
            payment += Mathf.Max(0, _vipPayment) * levelMultiplier;
            payment += Mathf.Max(0, customer.VipEventBonus);
        }
        else
            payment += Mathf.Max(0, _normalCustomerPayment);

        return payment;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ReloadGoldForCurrentPlayer();
    }

    public static void ClearSessionCache()
    {
        _sessionGold = null;
        _sessionPlayerName = null;
    }

    public void ReloadGoldForCurrentPlayer()
    {
        string playerName = null;
        PlayerProfileStorage.TryLoadLastPlayerName(out playerName);

        if (_sessionGold.HasValue
            && !string.IsNullOrEmpty(_sessionPlayerName)
            && string.Equals(_sessionPlayerName, playerName, System.StringComparison.OrdinalIgnoreCase))
        {
            _currentGold = _sessionGold.Value;
            return;
        }

        _currentGold = string.IsNullOrEmpty(playerName)
            ? 0
            : PlayerProfileStorage.LoadGoldForPlayer(playerName);

        _sessionGold = _currentGold;
        _sessionPlayerName = playerName;
    }

    private void Start()
    {
        GameEvents.RaiseGoldChanged(_currentGold);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool CanAfford(int amount)
    {
        return amount <= 0 || _currentGold >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        if (_currentGold < amount)
        {
            UIManager.Instance?.ShowNotEnoughMoneyFeedback();
            return false;
        }

        _currentGold -= amount;
        PersistGold();
        GameEvents.RaiseGoldChanged(_currentGold);
        return true;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        _currentGold += amount;

        if (_trackBusinessSessionIncome)
            _businessSessionIncome += amount;

        PersistGold();
        GameEvents.RaiseGoldChanged(_currentGold);
    }

    public void BeginBusinessSessionIncomeTracking()
    {
        _businessSessionIncome = 0;
        _trackBusinessSessionIncome = true;
    }

    public void StopBusinessSessionIncomeTracking()
    {
        _trackBusinessSessionIncome = false;
    }

    public void ResetBusinessSessionIncome()
    {
        _businessSessionIncome = 0;
        _trackBusinessSessionIncome = false;
    }

    private void PersistGold()
    {
        _sessionGold = _currentGold;

        if (PlayerProfileStorage.HasCurrentPlayerName)
            _sessionPlayerName = PlayerProfileStorage.CurrentPlayerName;

        PlayerProfileStorage.SaveGoldForCurrentPlayer(_currentGold);
    }
}
