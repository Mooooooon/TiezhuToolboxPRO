using System.Text.Json;

namespace TiezhuToolbox.Modules.Optimizer;

public sealed class OptimizerPresetRepository
{
    public OptimizerPresetDocument Load()
    {
        if (!File.Exists(AppPaths.OptimizerPresetsPath))
            return new OptimizerPresetDocument();
        try
        {
            var document = JsonSerializer.Deserialize<OptimizerPresetDocument>(
                File.ReadAllText(AppPaths.OptimizerPresetsPath), AppPaths.JsonOptions);
            return document?.SchemaVersion == OptimizerPresetDocument.CurrentSchemaVersion
                ? document
                : new OptimizerPresetDocument();
        }
        catch
        {
            AppPaths.PreserveBrokenFile(AppPaths.OptimizerPresetsPath);
            return new OptimizerPresetDocument();
        }
    }

    public void Save(OptimizerPresetDocument document)
    {
        if (File.Exists(AppPaths.OptimizerPresetsPath))
            File.Copy(AppPaths.OptimizerPresetsPath, AppPaths.OptimizerPresetsPath + ".bak", overwrite: true);
        AppPaths.WriteJsonAtomic(AppPaths.OptimizerPresetsPath, document);
    }
}
