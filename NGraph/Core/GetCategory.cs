namespace NGraph.Core;

public static class GetCategory
{
    //OrderBy(k => k.Value).First(). === MinBy
    //OrderByDescending(k => k.Value).First(). === MaxBy

    /// <summary>
    /// Before 2022  Category.BuiltInCategory  is empty this
    /// Code from 
    /// https://adn-cis.org/kak-poluchit-znachenie-builtincategory-dlya-obekta-klassa-category.html
    /// </summary>
    public static BuiltInCategory GetBuiltInCategory(this Category? category)
    {

#if REVIT2019
         if (category != null)
         {
             var cat = category.GetHashCode();
             if (Enum.IsDefined(typeof(BuiltInCategory), cat))
             {
                 var builtInCategory = (BuiltInCategory)cat;
                 return builtInCategory;
             }
             return BuiltInCategory.INVALID;
         }
         return BuiltInCategory.INVALID;

#elif REVIT2022

        if (category != null)
        {
            BuiltInCategory enumCategory = (BuiltInCategory)category.Id.IntegerValue;
            return enumCategory;
        }
        return BuiltInCategory.INVALID;

#else // 2023 and greater
        return category.BuiltInCategory;

#endif
    }
    
}