using System;
using System.Collections.Generic;
using System.Linq;
using HOVS.Model;

namespace HOVS.Plugin;

public static class ConnectionRules
{
    private static readonly string[] ExcludedTypes =
    {
        "пд дв", "противодым", "кондиционер", "увлажнитель",
        "воздушно-тепловая завеса", "втз", "аво", "агрегат воздушного отопления"
    };

    public static IReadOnlyList<Relation> Find(
        IReadOnlyList<Equipment> equipment,
        IReadOnlyList<EquipmentComponent> components)
    {
        var result = new List<Relation>();

        var candidates = equipment.Where(e => !IsExcluded(e)).ToList();
        var supplies = candidates.Where(IsSupplyOnly).ToList();
        var exhausts = candidates.Where(IsExhaustOnly).ToList();

        foreach (var p in supplies)
        foreach (var v in exhausts)
        {
            var pRecovery = GetRecoveryText(p, components);
            var vRecovery = GetRecoveryText(v, components);
            var hasRecovery = !string.IsNullOrWhiteSpace(pRecovery) ||
                              !string.IsNullOrWhiteSpace(vRecovery);

            var sameRoom =
                !string.IsNullOrWhiteSpace(p.Room) &&
                p.Room.Equals(v.Room, StringComparison.OrdinalIgnoreCase);

            var sameCore = PairCore(p.Id)
                .Equals(PairCore(v.Id), StringComparison.OrdinalIgnoreCase);

            var sameFinalCode = FinalCode(p.Id)
                .Equals(FinalCode(v.Id), StringComparison.OrdinalIgnoreCase);

            if (hasRecovery)
            {
                var score = 0.0;
                var reasons = new List<string>();

                if (sameCore)
                {
                    score += 0.70;
                    reasons.Add("совпадает код пары после П/В");
                }
                if (sameFinalCode)
                {
                    score += 0.15;
                    reasons.Add("совпадает конечный код");
                }
                if (sameRoom)
                {
                    score += 0.10;
                    reasons.Add("одно помещение");
                }

                score += !string.IsNullOrWhiteSpace(pRecovery) &&
                         !string.IsNullOrWhiteSpace(vRecovery)
                    ? 0.05
                    : 0.03;

                if (score >= 0.68)
                {
                    result.Add(new Relation(
                        p.Id,
                        v.Id,
                        InferRecoveryKind(pRecovery + " " + vRecovery),
                        Math.Min(score, 0.98),
                        "Кандидат связи по рекуператору: " + string.Join(", ", reasons)));
                }

                continue;
            }

            if (sameRoom && sameCore)
            {
                result.Add(new Relation(
                    p.Id,
                    v.Id,
                    RelationKind.SameRoom,
                    0.45,
                    "Общее помещение и совпадающий код пары; рекуператор явно не указан."));
            }
        }

        return result;
    }

    private static bool IsExcluded(Equipment e)
    {
        foreach (var x in ExcludedTypes)
            if (ContainsIgnoreCase(e.Type, x))
                return true;

        return false;
    }

    private static bool IsCombinedSupplyExhaust(Equipment e)
    {
        var id = (e.Id ?? "").TrimStart();
        return id.StartsWith("ПВ", StringComparison.OrdinalIgnoreCase) ||
               id.StartsWith("PV", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupplyOnly(Equipment e)
    {
        if (string.Equals(e.Type, "П", StringComparison.OrdinalIgnoreCase)) return true;
        if (IsControlledNonStreamType(e.Type)) return false;
        if (IsCombinedSupplyExhaust(e)) return false;
        var id = (e.Id ?? "").TrimStart();
        return id.StartsWith("П", StringComparison.OrdinalIgnoreCase) ||
               id.StartsWith("P", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExhaustOnly(Equipment e)
    {
        if (string.Equals(e.Type, "В", StringComparison.OrdinalIgnoreCase)) return true;
        if (IsControlledNonStreamType(e.Type)) return false;
        if (IsCombinedSupplyExhaust(e)) return false;
        var id = (e.Id ?? "").TrimStart();
        return id.StartsWith("В", StringComparison.OrdinalIgnoreCase) ||
               id.StartsWith("V", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsControlledNonStreamType(string type)
    {
        return string.Equals(type, "ПВУ", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "ВТЗ", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "АВО", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "Другая", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRecoveryText(
        Equipment equipment,
        IReadOnlyList<EquipmentComponent> components)
    {
        var values = components
            .Where(c =>
                c.EquipmentId.Equals(equipment.Id, StringComparison.OrdinalIgnoreCase) &&
                c.ComponentType.Equals("Рекуператор", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join(" ", values);
    }

    internal static string PairCore(string id)
    {
        var value = (id ?? "").Trim();
        if (value.Length == 0) return "";

        // П1.ПОН1.К2 -> ПОН1.К2
        // В1.ПОН1.К2 -> ПОН1.К2
        // P1.AHU.K2   -> AHU.K2
        var index = 0;
        if (value[index] == 'П' || value[index] == 'п' ||
            value[index] == 'В' || value[index] == 'в' ||
            value[index] == 'P' || value[index] == 'p' ||
            value[index] == 'V' || value[index] == 'v')
        {
            index++;
        }

        while (index < value.Length && char.IsDigit(value[index]))
            index++;

        while (index < value.Length &&
               (value[index] == '.' || value[index] == '-' ||
                value[index] == '_' || char.IsWhiteSpace(value[index])))
        {
            index++;
        }

        return index < value.Length ? value.Substring(index).Trim() : value;
    }

    internal static string FinalCode(string id)
    {
        var value = (id ?? "").Trim();
        if (value.Length == 0) return "";
        var parts = value.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? value : parts[parts.Length - 1].Trim();
    }

    internal static RelationKind InferRecoveryKind(string text)
    {
        if (ContainsIgnoreCase(text, "гликол"))
            return RelationKind.GlycolicHeatRecovery;
        if (ContainsIgnoreCase(text, "пластин"))
            return RelationKind.PlateHeatRecovery;
        if (ContainsIgnoreCase(text, "ротор"))
            return RelationKind.RotaryHeatRecovery;
        return RelationKind.None;
    }

    internal static bool ContainsIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text) &&
               !string.IsNullOrEmpty(value) &&
               text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
