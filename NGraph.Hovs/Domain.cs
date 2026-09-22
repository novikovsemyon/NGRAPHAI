using System;
using System.Collections.Generic;
using System.Linq;
namespace HOVS.Model;

public enum RelationKind
{
    None,
    GlycolicHeatRecovery,
    PlateHeatRecovery,
    RotaryHeatRecovery,
    Recirculation,
    SameRoom
}

public sealed class Equipment
{
    public string Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string Room { get; }
    public Dictionary<string,string> Attributes { get; }

    public Equipment(string id,string name,string type,string room,Dictionary<string,string> attributes)
    {
        Id=id; Name=name; Type=type; Room=room; Attributes=attributes;
    }
}

public sealed class EquipmentComponent
{
    public string EquipmentId { get; }
    public string ComponentType { get; }
    public string Value { get; }
    public int? Quantity { get; }

    public EquipmentComponent(string equipmentId,string componentType,string value,int? quantity)
    {
        EquipmentId=equipmentId; ComponentType=componentType; Value=value; Quantity=quantity;
    }
}

public sealed class Relation
{
    public string SourceId { get; }
    public string TargetId { get; }
    public RelationKind Kind { get; }
    public double Confidence { get; }
    public string Reason { get; }

    public Relation(string sourceId,string targetId,RelationKind kind,double confidence,string reason)
    {
        SourceId=sourceId; TargetId=targetId; Kind=kind; Confidence=confidence; Reason=reason;
    }
}

public sealed class HovsModel
{
    public IReadOnlyList<Equipment> Equipment { get; }
    public IReadOnlyList<EquipmentComponent> Components { get; }
    public IReadOnlyList<Relation> Relations { get; }

    public HovsModel(IReadOnlyList<Equipment> equipment,
                     IReadOnlyList<EquipmentComponent> components,
                     IReadOnlyList<Relation> relations)
    {
        Equipment=equipment; Components=components; Relations=relations;
    }
}
