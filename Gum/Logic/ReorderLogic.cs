﻿using Gum.DataTypes;
using Gum.Managers;
using Gum.Plugins;
using Gum.ToolStates;
using Gum.Wireframe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gum.Logic
{
    public class ReorderLogic : Singleton<ReorderLogic>
    {
        public void MoveSelectedInstanceForward()
        {
            var instance = SelectedState.Self.SelectedInstance;
            var element = SelectedState.Self.SelectedElement;

            if (instance != null)
            {
                var siblingInstances = instance.GetSiblingsIncludingThis();
                var thisIndex = siblingInstances.IndexOf(instance);
                bool isLast = thisIndex == siblingInstances.Count - 1;

                if (!isLast)
                {
                    // remove it before getting the new index, or else the removal could impact the
                    // index.
                    element.Instances.Remove(instance);
                    var nextSibling = siblingInstances[thisIndex + 1];

                    var nextSiblingIndexInContainer = element.Instances.IndexOf(nextSibling);

                    element.Instances.Insert(nextSiblingIndexInContainer + 1, instance);
                    RefreshInResponseToReorder(instance);
                }
            }
        }

        public void MoveSelectedInstanceBackward()
        {
            var instance = SelectedState.Self.SelectedInstance;
            var element = SelectedState.Self.SelectedElement;

            if (instance != null)
            {
                // remove it before getting the new index, or else the removal could impact the
                // index.
                var siblingInstances = instance.GetSiblingsIncludingThis();
                var thisIndex = siblingInstances.IndexOf(instance);
                bool isFirst = thisIndex == 0;

                if (!isFirst)
                {
                    element.Instances.Remove(instance);
                    var previousSibling = siblingInstances[thisIndex - 1];

                    var previousSiblingIndexInContainer = element.Instances.IndexOf(previousSibling);

                    element.Instances.Insert(previousSiblingIndexInContainer, instance);
                    RefreshInResponseToReorder(instance);
                }
            }
        }

        public void MoveSelectedInstanceToFront()
        {
            InstanceSave instance = SelectedState.Self.SelectedInstance;
            ElementSave element = SelectedState.Self.SelectedElement;

            if (instance != null)
            {
                // to bring to back, we're going to remove, then add (at the end)
                element.Instances.Remove(instance);
                element.Instances.Add(instance);

                RefreshInResponseToReorder(instance);
            }
        }

        public void MoveSelectedInstanceToBack()
        {
            InstanceSave instance = SelectedState.Self.SelectedInstance;
            ElementSave element = SelectedState.Self.SelectedElement;

            if (instance != null)
            {
                // to bring to back, we're going to remove, then insert at index 0
                element.Instances.Remove(instance);
                element.Instances.Insert(0, instance);

                RefreshInResponseToReorder(instance);
            }
        }

        public void MoveSelectedInstanceInFrontOf(InstanceSave whatToMoveInFrontOf)
        {
            var element = SelectedState.Self.SelectedElement;
            var whatToInsert = SelectedState.Self.SelectedInstance;
            if (whatToInsert != null)
            {
                element.Instances.Remove(whatToInsert);
                int whereToInsert = element.Instances.IndexOf(whatToMoveInFrontOf) + 1;

                element.Instances.Insert(whereToInsert, whatToInsert);

                RefreshInResponseToReorder(whatToMoveInFrontOf);

                if (ProjectManager.Self.GeneralSettingsFile.AutoSave)
                {
                    ProjectManager.Self.SaveElement(element);
                }
            }
        }
        private void RefreshInResponseToReorder(InstanceSave instance)
        {
            var element = SelectedState.Self.SelectedElement;

            GumCommands.Self.GuiCommands.RefreshElementTreeView(element);


            WireframeObjectManager.Self.RefreshAll(true);

            SelectionManager.Self.Refresh();
            GumCommands.Self.FileCommands.TryAutoSaveCurrentElement();

            PluginManager.Self.InstanceReordered(instance);
        }
    }
}
