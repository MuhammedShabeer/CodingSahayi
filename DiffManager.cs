using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace CodingSahayi;

public static class DiffManager
{
    public static SideBySideDiffModel GenerateDiff(string oldText, string newText)
    {
        var diffBuilder = new SideBySideDiffBuilder(new Differ());
        return diffBuilder.BuildDiffModel(oldText ?? "", newText ?? "");
    }
}
