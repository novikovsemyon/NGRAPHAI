namespace NGraph.Core;

public static class GetCategory
{
    /// <summary>
    /// Returns the Revit built-in category or <see cref="BuiltInCategory.INVALID"/> when no category is available.
    /// Revit 2021-2022 require reading the category id; newer versions expose BuiltInCategory directly.
    /// </summary>
    public static BuiltInCategory GetBuiltInCategory(this Category? category)
    {
        if (category is null)
        {
            return BuiltInCategory.INVALID;
        }

#if REVIT2021 || REVIT2022
        return (BuiltInCategory)category.Id.IntegerValue;
#else
        return category.BuiltInCategory;
#endif
    }
}
