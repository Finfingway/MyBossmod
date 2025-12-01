namespace BossMod.Endwalker.Ultimate.TOP;

sealed class P3OversampledWaveCannonSafe : BossComponent
{
    private Actor? _boss;
    private Angle _bossAngle;
    private readonly Angle[] _playerAngles = new Angle[PartyState.MaxPartySize];
    private readonly int[] _playerOrder = new int[PartyState.MaxPartySize];
    private int _numPlayerAngles;

    private static readonly Dictionary<string, WPos> BasePositions = new()
    {
        // 中央
        ["Center"] = new WPos(100, 100),
        // 北
        ["NorthNear"] = new WPos(100, 90.5f),
        ["NorthFar"]  = new WPos(100, 81.0f),
        // 南
        ["SouthNear"] = new WPos(100, 109.5f),
        ["SouthFar"]  = new WPos(100, 119.0f),
        // 東
        ["EastNear"]  = new WPos(109.5f, 100),
        ["EastFar"]   = new WPos(119.0f, 100),
        // 西
        ["WestNear"]  = new WPos(90.5f, 100),
        ["WestFar"]   = new WPos(81.0f, 100),
    };

    private readonly TOPConfig _config = Service.Config.Get<TOPConfig>();

    private bool IsTH(int slot) => Raid.Roles[slot] is Role.Tank or Role.Healer;
    private bool IsDPS(int slot) => Raid.Roles[slot] is Role.Melee or Role.Ranged;
    private bool IsMonitor(int slot) => _playerAngles[slot] != default;

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        // Monitor 状態の登録（既存ロジック）
        var angle = status.ID switch
        {
            (uint)SID.OversampledWaveCannonLoadingL => 90f.Degrees(),
            (uint)SID.OversampledWaveCannonLoadingR => -90f.Degrees(),
            _ => default
        };

        if (angle != default && Raid.FindSlot(actor.InstanceID) is var slot && slot >= 0)
        {
            _playerAngles[slot] = angle;
            if (++_numPlayerAngles == 3)
                AssignPlayerOrder();
        }
    }

    private void AssignPlayerOrder()
    {
        // Monitor 人数が 3 人になったら順番を決める
        int n = 0, m = 0;
        foreach (var sg in _config.P3MonitorsAssignments.Resolve(Raid).OrderBy(sg => sg.group))
        {
            _playerOrder[sg.slot] = IsMonitor(sg.slot) ? ++m : ++n;
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var (pos, assigned) in SafeSpots(pcSlot))
            Arena.AddCircle(pos, 1f, assigned ? Colors.Safe : default);
    }

    private List<(WPos pos, bool assigned)> SafeSpots(int slot)
    {
        var safespots = new List<(WPos, bool)>();

        if (_numPlayerAngles < 3 || _bossAngle == default)
            return safespots;

        // ロールごとのグループを作る
        var thGroup = Raid.WithSlot().Where(p => IsTH(p.Slot)).ToList();
        var dpsGroup = Raid.WithSlot().Where(p => IsDPS(p.Slot)).ToList();

        // 各グループで SafeSpot を割り当て
        safespots.AddRange(AssignGroupSafeSpots(thGroup, slot));
        safespots.AddRange(AssignGroupSafeSpots(dpsGroup, slot));

        return safespots;
    }

    private List<(WPos pos, bool assigned)> AssignGroupSafeSpots(List<Actor> group, int slot)
    {
        var spots = new List<(WPos, bool)>();
        int monitorCount = group.Count(p => IsMonitor(p.Slot));

        // TH/DPS 内で Monitor 数に応じて座標割り当て
        for (int i = 0; i < group.Count; i++)
        {
            var p = group[i];
            WPos pos = BasePositions["Center"]; // 初期は中央

            // Monitor 調整ルール
            if (monitorCount == 0)
            {
                // そのまま
                pos = BasePositions["Center"];
            }
            else if (monitorCount == 1)
            {
                // 横軸 1 人になるよう調整
                pos = (i == 0) ? BasePositions["WestNear"] : BasePositions["EastNear"];
            }
            else if (monitorCount == 2)
            {
                // 横 1 / 縦 1 に調整
                pos = (i == 0) ? BasePositions["WestNear"] : BasePositions["NorthNear"];
            }
            else if (monitorCount == 3)
            {
                // 横 2 / 縦 1
                if (i == 0) pos = BasePositions["WestNear"];
                else if (i == 1) pos = BasePositions["EastNear"];
                else pos = BasePositions["NorthFar"];
            }

            spots.Add((pos, p.Slot == slot));
        }

        return spots;
    }
}

