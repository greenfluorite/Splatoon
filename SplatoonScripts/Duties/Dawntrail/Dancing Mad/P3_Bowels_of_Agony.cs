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

public unsafe class P2_Bowels_of_Agony : SplatoonScript
{
    public override Metadata Metadata { get; } = new(1, "Author");
    public override HashSet<uint>? ValidTerritories { get; } = [1363];

    // --- Crystal DataIDs ---
    const uint DataID_Fire  = 0xEC03A; // 966714
    const uint DataID_Water = 0xEC03B; // 966715
    const uint DataID_Wind  = 0xEC03C; // 966716

    // --- Debuff Status IDs ---
    const uint Buff_Entropy      = 1600; // Fire  -> Fire crystal
    const uint Buff_DynamicFluid = 1601; // Water -> Water crystal
    const uint Buff_Headwind     = 1602; // Wind  -> Wind crystal
    const uint Buff_Tailwind     = 1603; // Wind  -> Wind crystal

    // --- Trigger ---
    const uint Action_BowelsOfAgony = 47858;

    // Arena center
    static readonly Vector2 Center = new(100f, 100f);

    // How far from the crystal to offset support (out) and DPS (in)
    const float SupportOffset = 3.0f;  // away from center
    const float DpsOffset     = -2.0f; // toward center (negative = inward)

    bool _active = false;

    public override void OnSetup()
    {
        // One marker per player (8 players)
        for (int i = 0; i < 8; i++)
        {
            Controller.RegisterElementFromCode($"Marker{i}", """
                {"Name":"","type":1,"radius":0.8,"color":3355508735,"Filled":true,"fillIntensity":0.4,"overlayVOffset":1.5,"thicc":3.0,"overlayText":"GO HERE","refActorComparisonType":2}
                """);
            Controller.RegisterElementFromCode($"Tether{i}", """
                {"Name":"","type":3,"radius":0.0,"color":3372220160,"Filled":false,"fillIntensity":0.5,"thicc":2.0,"refActorComparisonType":2}
                """);
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action?.RowId == Action_BowelsOfAgony)
        {
            _active = true;
        }
    }

    public override void OnUpdate()
    {
        Controller.Hide();
        if (!_active) return;

        // --- Find crystals ---
        var windCrystal  = FindActorByDataID(DataID_Wind);
        var fireCrystal  = FindActorByDataID(DataID_Fire);
        var waterCrystal = FindActorByDataID(DataID_Water);

        // Wait until all 3 have spawned
        if (windCrystal == null || fireCrystal == null || waterCrystal == null) return;

        var windPos  = new Vector2(windCrystal->Position.X,  windCrystal->Position.Z);
        var firePos  = new Vector2(fireCrystal->Position.X,  fireCrystal->Position.Z);
        var waterPos = new Vector2(waterCrystal->Position.X, waterCrystal->Position.Z);

        int markerIdx = 0;
        int tetherIdx = 0;

        var members = Controller.GetPartyMembers();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            bool isSupport = (i < 4); // slots 0-3 = supports, 4-7 = DPS

            // Determine target crystal position based on debuff
            Vector2? targetPos = GetTargetPosition(member, windPos, firePos, waterPos, isSupport);
            if (targetPos == null) continue;

            // Draw marker at target position
            if (Controller.TryGetElementByName($"Marker{markerIdx}", out var markerEl))
            {
                markerEl.Enabled = true;
                markerEl.RefPosition = new Vector3(targetPos.Value.X, 0f, targetPos.Value.Y);
            }

            // Draw tether from player to target
            if (Controller.TryGetElementByName($"Tether{tetherIdx}", out var tetherEl))
            {
                tetherEl.Enabled = true;
                tetherEl.refActorObjectID = member.ObjectId;
                tetherEl.refActorComparisonType = 2;
                tetherEl.tether = true;
                tetherEl.RefPosition = new Vector3(targetPos.Value.X, 0f, targetPos.Value.Y);
            }

            markerIdx++;
            tetherIdx++;
        }
    }

    Vector2? GetTargetPosition(
        Splatoon.SplatoonScripting.IPartyMember member,
        Vector2 windPos, Vector2 firePos, Vector2 waterPos,
        bool isSupport)
    {
        bool hasEntropy      = member.StatusList.Any(s => s.StatusId == Buff_Entropy);
        bool hasDynamicFluid = member.StatusList.Any(s => s.StatusId == Buff_DynamicFluid);
        bool hasWindDebuff   = member.StatusList.Any(s => s.StatusId == Buff_Headwind || s.StatusId == Buff_Tailwind);

        Vector2 crystalPos;

        if (hasEntropy)
            crystalPos = firePos;
        else if (hasDynamicFluid)
            crystalPos = waterPos;
        else if (hasWindDebuff)
            crystalPos = windPos;
        else
            return null; // no relevant debuff

        return OffsetPosition(crystalPos, isSupport);
    }

    /// <summary>
    /// Offset a position relative to the crystal.
    /// Supports go outward (away from center), DPS go inward (toward center).
    /// </summary>
    Vector2 OffsetPosition(Vector2 crystalPos, bool isSupport)
    {
        // Direction from center to crystal (normalized)
        var dir = Vector2.Normalize(crystalPos - Center);
        float offset = isSupport ? SupportOffset : DpsOffset;
        return crystalPos + dir * offset;
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
