using System;
using System.Collections.Generic;

[Serializable]
public enum PackCreatorAssetGroup
{
    Agents = 0,
    World = 1,
    UI = 2
}

[Serializable]
public enum PackCreatorBuildStyle
{
    BasicShapes = 1,
    JsonBlueprint = 2,
    SheetIso4Dir = 4,
    SheetIso8Dir = 5
}

[Serializable]
public sealed class PackCreatorSimConfig
{
    public string simulationId;
    public string defaultEntityId;
    public List<string> defaultStates = new List<string>();

    public PackCreatorSimConfig(string simulationId, string defaultEntityId, IReadOnlyList<string> defaultStates)
    {
        this.simulationId = simulationId;
        this.defaultEntityId = defaultEntityId;
        this.defaultStates = new List<string>(defaultStates ?? Array.Empty<string>());
    }
}

[Serializable]
public sealed class PackCreatorSheetImportRow
{
    public string sourcePath;
    public string sourceFileName;
    public string guessedState;
    public string entityId;
    public string state;
    public int dirSet;
}
