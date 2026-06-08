using ECommons.GameFunctions;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Splatoon;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.Dancing_Mad;

public unsafe class P3_Bowels_of_Agony : SplatoonScript
{
    public override Metadata Metadata { get; } = new(1, "greenfluorite");
    public override HashSet<uint>? ValidTerritories { get; } = [1363];

    // Crystal DataIDs
    const uint DataID_Fire  = 0xEC03A;
    const uint DataID_Water = 0xEC03B;
    const uint DataID_Wind  = 0xEC03C;

    // Debuff Status IDs
    const uint Buff_Entropy      = 1600;
    const uint Buff_DynamicFluid = 1601;
    const uint Buff_Headwind     = 1602;
    const uint Buff_Tailwind     = 1603;

    // Trigger action
    const uint Action_BowelsOfAgony = 47858;

    // Arena center
    static readonly Vector2 Center = new Vector2(100f, 100f);

    // Offset distances: positive = away from center, negative = toward center
    const float SupportOffset = 3.0f;
    const float DpsOffset     = -2.0f;

    bool _active = false;

    public override void OnSetup()
    {
        for (int i = 0; i < 8; i++)
        {
            Controller.RegisterElementFromCode("Marker" + i,
                "{\"Name\":\"\",\"type\":1,\"radius\":0.8,\"color\":3355508735,\"Filled\":true,\"fillIntensity\":0.4,\"overlayVOffset\":1.5,\"thicc\":3.0,\"overlayText\":\"GO HERE\",\"refActorComparisonType\":2}");
            Controller.RegisterElementFromCode("Tether" + i,
                "{\"Name\":\"\",\"type\":3,\"radius\":0.0,\"color\":3372220160,\"Filled\":false,\"fillIntensity\":0.5,\"thicc\":2.0,\"refActorComparisonType\":2}");
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action != null && set.Action.Value.RowId == Action_BowelsOfAgony)
        {
            _active = true;
        }
    }

    public override void OnUpdate()
    {
        Controller.Hide();
        if (!_active) return;

        var windCrystal  = FindActorByDataID(DataID_Wind);
        var fireCrystal  = FindActorByDataID(DataID_Fire);
        var waterCrystal = FindActorByDataID(DataID_Water);

        if (windCrystal == null || fireCrystal == null || waterCrystal == null) return;

        var windPos  = new Vector2(windCrystal->Position.X,  windCrystal->Position.Z);
        var firePos  = new Vector2(fireCrystal->Position.X,  fireCrystal->Position.Z);
        var waterPos = new Vector2(waterCrystal->Position.X, waterCrystal->Position.Z);

        int idx = 0;
        var members = Controller.GetPartyMembers();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            bool isSupport = (i < 4);

            bool hasEntropy      = member.StatusList.Any(s => s.StatusId == Buff_Entropy);
            bool hasDynamicFluid = member.StatusList.Any(s => s.StatusId == Buff_DynamicFluid);
            bool hasWindDebuff   = member.StatusList.Any(s => s.StatusId == Buff_Headwind || s.StatusId == Buff_Tailwind);

            Vector2 crystalPos;
            if      (hasEntropy)      crystalPos = firePos;
            else if (hasDynamicFluid) crystalPos = waterPos;
            else if (hasWindDebuff)   crystalPos = windPos;
            else continue;

            var dir    = Vector2.Normalize(crystalPos - Center);
            float off  = isSupport ? SupportOffset : DpsOffset;
            var target = crystalPos + dir * off;
            var pos3   = new Vector3(target.X, 0f, target.Y);

            if (Controller.TryGetElementByName("Marker" + idx, out var markerEl))
            {
                markerEl.Enabled     = true;
                markerEl.RefPosition = pos3;
            }

            if (Controller.TryGetElementByName("Tether" + idx, out var tetherEl))
            {
                tetherEl.Enabled              = true;
                tetherEl.refActorObjectID     = member.ObjectId;
                tetherEl.refActorComparisonType = 2;
                tetherEl.tether               = true;
                tetherEl.RefPosition          = pos3;
            }

            idx++;
        }
    }

    FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* FindActorByDataID(uint dataId)
    {
        foreach (var obj in Splatoon.Memory.MemoryManager.GetObjectTable())
        {
            if (obj == null) continue;
            if (obj->DataID == dataId) return obj;
        }
        return null;
    }

    public override void OnReset()
    {
        _active = false;
        Controller.Hide();
    }
}
