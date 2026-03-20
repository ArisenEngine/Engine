using ArisenEngine.Core.ECS;
using ArisenEditor.Core.Services;
using ArisenKernel.Contracts;

namespace ArisenEditor.Core.Commands;

/// <summary>
/// Command to rename an entity's NameComponent.
/// Stores old name for undo.
/// </summary>
public class RenameEntityCommand : ICommand
{
    private readonly Entity m_Entity;
    private readonly string m_OldName;
    private readonly string m_NewName;

    public string Description => $"Rename Entity '{m_OldName}' 鈫?'{m_NewName}'";

    public RenameEntityCommand(Entity entity, string oldName, string newName)
    {
        m_Entity = entity;
        m_OldName = oldName;
        m_NewName = newName;
    }

    public void Execute()
    {
        SetName(m_NewName);
    }

    public void Undo()
    {
        SetName(m_OldName);
    }

    private void SetName(string name)
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null) return;

        if (scene.Registry.HasComponent<NameComponent>(m_Entity))
        {
            ref var comp = ref scene.Registry.GetComponent<NameComponent>(m_Entity);
            comp.Name = name;
            SceneManagerService.Instance.NotifyEntityNameChanged(m_Entity, name);
        }
    }
}

