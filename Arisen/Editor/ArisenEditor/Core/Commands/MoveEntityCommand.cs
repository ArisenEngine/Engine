using ArisenEngine.Core.ECS;
using ArisenEditor.Core.Services;
using ArisenKernel.Contracts;

namespace ArisenEditor.Core.Commands;

/// <summary>
/// Command to reparent an entity in the hierarchy.
/// Handles the intrusive linked list (Parent/Child/Sibling components)
/// and supports undo by restoring the previous parent.
/// </summary>
public class MoveEntityCommand : ICommand
{
    private readonly Entity m_Entity;
    private readonly Entity m_NewParent;
    private readonly Entity m_OldParent;
    private readonly bool m_HadOldParent;

    public string Description => $"Move Entity {m_Entity.Id} 鈫?Parent {(m_NewParent == Entity.Null ? "Root" : m_NewParent.Id.ToString())}";

    public MoveEntityCommand(Entity entity, Entity newParent, EntityManager em)
    {
        m_Entity = entity;
        m_NewParent = newParent;
        m_HadOldParent = em.HasComponent<ParentComponent>(entity);
        m_OldParent = m_HadOldParent ? em.GetComponent<ParentComponent>(entity).Parent : Entity.Null;
    }

    public void Execute()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null) return;
        ReparentEntity(scene.Registry, m_Entity, m_NewParent);
        SceneManagerService.Instance.NotifyEntityParentChanged(m_Entity, m_NewParent);
    }

    public void Undo()
    {
        var scene = SceneManagerService.Instance.ActiveScene;
        if (scene == null) return;
        ReparentEntity(scene.Registry, m_Entity, m_OldParent);
        SceneManagerService.Instance.NotifyEntityParentChanged(m_Entity, m_OldParent);
    }

    /// <summary>
    /// Core reparenting logic extracted from HierarchyViewModel.MoveEntity().
    /// Handles detaching from old parent's linked list and inserting into new parent.
    /// </summary>
    public static void ReparentEntity(EntityManager em, Entity srcEntity, Entity newParentEntity)
    {
        // 1. Detach from old parent's sibling linked list
        if (em.HasComponent<ParentComponent>(srcEntity))
        {
            var oldParentComp = em.GetComponent<ParentComponent>(srcEntity);
            var oldParent = oldParentComp.Parent;

            if (em.HasComponent<SiblingComponent>(srcEntity))
            {
                var sibling = em.GetComponent<SiblingComponent>(srcEntity);
                
                if (sibling.PrevSibling != Entity.Null)
                {
                    ref var prevSiblingComponent = ref em.GetComponent<SiblingComponent>(sibling.PrevSibling);
                    prevSiblingComponent.NextSibling = sibling.NextSibling;
                }
                else if (oldParent != Entity.Null && em.HasComponent<ChildComponent>(oldParent))
                {
                    ref var oldParentChildComp = ref em.GetComponent<ChildComponent>(oldParent);
                    oldParentChildComp.FirstChild = sibling.NextSibling;
                }

                if (sibling.NextSibling != Entity.Null)
                {
                    ref var nextSiblingComponent = ref em.GetComponent<SiblingComponent>(sibling.NextSibling);
                    nextSiblingComponent.PrevSibling = sibling.PrevSibling;
                }

                em.RemoveComponent<SiblingComponent>(srcEntity);
            }

            if (oldParent != Entity.Null && em.HasComponent<ChildComponent>(oldParent))
            {
                ref var oldParentChildComp = ref em.GetComponent<ChildComponent>(oldParent);
                oldParentChildComp.ChildCount--;
                if (oldParentChildComp.ChildCount <= 0)
                    em.RemoveComponent<ChildComponent>(oldParent);
            }
        }

        // 2. Assign to new parent
        if (newParentEntity != Entity.Null)
        {
            if (em.HasComponent<ParentComponent>(srcEntity))
            {
                ref var comp = ref em.GetComponent<ParentComponent>(srcEntity);
                comp.Parent = newParentEntity;
            }
            else
            {
                em.AddComponent(srcEntity, new ParentComponent { Parent = newParentEntity });
            }

            if (!em.HasComponent<ChildComponent>(newParentEntity))
            {
                em.AddComponent(newParentEntity, new ChildComponent { FirstChild = srcEntity, ChildCount = 1 });
                em.AddComponent(srcEntity, new SiblingComponent { NextSibling = Entity.Null, PrevSibling = Entity.Null });
            }
            else
            {
                ref var newParentChildComp = ref em.GetComponent<ChildComponent>(newParentEntity);
                newParentChildComp.ChildCount++;
                
                var currentChild = newParentChildComp.FirstChild;
                var lastChild = Entity.Null;
                
                // Find terminal child
                while (currentChild != Entity.Null)
                {
                    lastChild = currentChild;
                    if (em.HasComponent<SiblingComponent>(currentChild))
                    {
                        var next = em.GetComponent<SiblingComponent>(currentChild).NextSibling;
                        if (next == Entity.Null) break;
                        currentChild = next;
                    }
                    else
                        break;
                }
                
                // Append structurally
                if (lastChild != Entity.Null)
                {
                    if (em.HasComponent<SiblingComponent>(lastChild))
                    {
                        ref var lastChildSib = ref em.GetComponent<SiblingComponent>(lastChild);
                        lastChildSib.NextSibling = srcEntity;
                    }
                    else
                    {
                        em.AddComponent(lastChild, new SiblingComponent { NextSibling = srcEntity, PrevSibling = Entity.Null });
                    }
                }

                if (em.HasComponent<SiblingComponent>(srcEntity))
                {
                    ref var srcSib = ref em.GetComponent<SiblingComponent>(srcEntity);
                    srcSib.PrevSibling = lastChild;
                    srcSib.NextSibling = Entity.Null;
                }
                else
                {
                    em.AddComponent(srcEntity, new SiblingComponent { PrevSibling = lastChild, NextSibling = Entity.Null });
                }
            }
        }
        else
        {
            // Moving to root
            em.RemoveComponent<ParentComponent>(srcEntity);
            em.RemoveComponent<SiblingComponent>(srcEntity);
        }
    }
}

