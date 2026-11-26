namespace DevTools.ModelsContext.Documents;

public readonly record struct ElementRow(
    long Id,
    int GroupIndex,
    int TypeIndex,
    long LevelId,
    long WorksetId,
    bool SecondaryOption = false,
    ViewDraw Draw = ViewDraw.Model,
    long OwnerViewId = ViewDraws.NoOwner)
{
    public bool InWholeModel => !SecondaryOption && OwnerViewId == ViewDraws.NoOwner;
}

public readonly record struct TypeRow(long TypeId, int GroupIndex, int FamilyNameIndex, int TypeNameIndex);

public readonly record struct GroupRow(long GroupId, int NameIndex, long ParentId = GroupRow.TopLevel)
{
    public const long TopLevel = 0;

    public bool IsSubgroup => ParentId != TopLevel;
}

public readonly record struct ElementRecord(
    long Id,
    long TypeId,
    long LevelId,
    long WorksetId,
    bool SecondaryOption = false,
    ViewDraw Draw = ViewDraw.Model,
    long OwnerViewId = ViewDraws.NoOwner);

public sealed record TypeRecord(
    long TypeId,
    long GroupId,
    string GroupName,
    string FamilyName,
    string TypeName,
    long ParentId = GroupRow.TopLevel);
