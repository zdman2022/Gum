﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InputLibrary;
using Gum.DataTypes;
using System.Windows.Forms;
using Gum.Wireframe;
using ToolsUtilities;
using RenderingLibrary.Content;
using RenderingLibrary;
using Gum.Input;
using CommonFormsAndControls.Forms;
using Gum.ToolStates;
using Gum.DataTypes.Variables;
using Gum.ToolCommands;
using RenderingLibrary.Graphics;
using Gum.PropertyGridHelpers;
using System.Drawing;
using Gum.Converters;

namespace Gum.Managers
{
    public class DragDropManager
    {
        #region Fields

        static DragDropManager mSelf;

        object mDraggedItem;

        #endregion

        #region Properties

        public InputLibrary.Cursor Cursor
        {
            get { return InputLibrary.Cursor.Self; }
        }

        public static DragDropManager Self
        {
            get
            {
                if (mSelf == null)
                {
                    mSelf = new DragDropManager();
                }
                return mSelf;
            }
        }

        #endregion


        public void OnItemDrag(object item)
        {
            mDraggedItem = item;
        }

        public void Activity()
        {
            if (!Cursor.PrimaryDownIgnoringIsInWindow)
            {
                if (mDraggedItem != null)
                {
                    TreeNode draggedTreeNode = (TreeNode)mDraggedItem;
                    object draggedObject = draggedTreeNode.Tag;


                    mDraggedItem = null; // to prevent this from getting hit again if a message box is up


                    bool handled = false;
                    HandleDroppedItemInWireframe(draggedObject, out handled);

                    if (!handled)
                    {
                        TreeNode targetTreeNode = ElementTreeViewManager.Self.GetTreeNodeOver();
                        if (targetTreeNode != draggedTreeNode)
                        {
                            HandleDroppedItemOnTreeView(draggedObject, targetTreeNode);
                        }
                    }
                }
            }
        }
        

        private void HandleDroppedItemOnTreeView(object draggedComponentOrElement, TreeNode treeNodeDroppedOn)
        {
            Console.WriteLine($"Dropping{draggedComponentOrElement} on {treeNodeDroppedOn}");
            if (treeNodeDroppedOn != null)
            {
                object targetTag = treeNodeDroppedOn.Tag;

                if (draggedComponentOrElement is ElementSave)
                {
                    HandleDroppedElementSave(draggedComponentOrElement, treeNodeDroppedOn, targetTag);
                }
                else if (draggedComponentOrElement is InstanceSave)
                {
                    HandleDroppedInstance(draggedComponentOrElement, targetTag);
                }
            }
        }

        private void HandleDroppedElementSave(object draggedComponentOrElement, TreeNode treeNodeDroppedOn, object targetTag)
        {
            ElementSave draggedAsElementSave = draggedComponentOrElement as ElementSave;

            // User dragged an element save - so they want to take something like a
            // text object and make an instance in another element like a Screen
            bool handled;

            if (targetTag is ElementSave)
            {
                HandleDroppedElementInElement(draggedAsElementSave, targetTag as ElementSave, out handled);
            }
            else if (targetTag is InstanceSave)
            {
                // The user dropped it on an instance save, but he likely meant to drop
                // it as an object under the current element.

                InstanceSave targetInstance = targetTag as InstanceSave;


                HandleDroppedElementInElement(draggedAsElementSave, targetInstance.ParentContainer, out handled);
            }
            else if (treeNodeDroppedOn.IsTopComponentContainerTreeNode())
            {
                HandleDroppedElementOnTopComponentTreeNode(draggedAsElementSave, out handled);

            }
            else if (treeNodeDroppedOn.IsPartOfComponentsFolderStructure())
            {
                HandleDroppedElementOnFolder(draggedAsElementSave, treeNodeDroppedOn, out handled);
            }
            else
            {
                MessageBox.Show("You must drop " + draggedAsElementSave.Name + " on either a Screen or an Component");
            }
        }

        private void HandleDroppedElementOnFolder(ElementSave draggedAsElementSave, TreeNode treeNodeDroppedOn, out bool handled)
        {
            var fullFolderPath = treeNodeDroppedOn.GetFullFilePath();

            var fullElementFilePath = FileManager.GetDirectory( draggedAsElementSave.GetFullPathXmlFile());

            handled = false;

            if(FileManager.Standardize(fullFolderPath) != FileManager.Standardize(fullElementFilePath))
            {
                var projectFolder = FileManager.GetDirectory(ProjectManager.Self.GumProjectSave.FullFileName);

                string nodeRelativeToProject = FileManager.MakeRelative(fullFolderPath, projectFolder + draggedAsElementSave.Subfolder + "\\", preserveCase:true);

                string oldName = draggedAsElementSave.Name;
                draggedAsElementSave.Name = nodeRelativeToProject + FileManager.RemovePath(draggedAsElementSave.Name);
                RenameManager.Self.HandleRename(draggedAsElementSave, (InstanceSave)null, oldName);

                handled = true;
            }

        }

        private void HandleDroppedElementOnTopComponentTreeNode(ElementSave draggedAsElementSave, out bool handled)
        {
            handled = false;
            string name = draggedAsElementSave.Name;

            string currentDirectory = FileManager.GetDirectory(draggedAsElementSave.Name);

            if(!string.IsNullOrEmpty(currentDirectory))
            {
                // It's in a directory, we're going to move it out
                draggedAsElementSave.Name = FileManager.RemovePath(name);
                RenameManager.Self.HandleRename(draggedAsElementSave, (InstanceSave)null, name);

                handled = true;
            }
        }

        private static void HandleDroppedInstance(object draggedObject, object targetTag)
        {
            InstanceSave draggedAsInstanceSave = draggedObject as InstanceSave;

            ElementSave targetElementSave = targetTag as ElementSave;
            if (targetElementSave == null && targetTag is InstanceSave)
            {
                targetElementSave = ((InstanceSave)targetTag).ParentContainer;
            }


            bool isSameElement = draggedAsInstanceSave != null && targetElementSave == draggedAsInstanceSave.ParentContainer;

            if (targetElementSave != null)
            {
                // We aren't going to allow drag+drop within the same element - that may cause
                // unexpected copy/paste if the user didn't mean to drag, and we may want to support
                // reordering.
                if (isSameElement)
                {
                    if(targetTag != draggedAsInstanceSave)
                    {

                    }
                    string parentName;
                    string variableName = draggedAsInstanceSave.Name + ".Parent";
                    if(targetTag is InstanceSave)
                    {
                        // setting the parent:
                        parentName = (targetTag as InstanceSave).Name;


                    }
                    else
                    {
                        // drag+drop on the container, so detach:
                        parentName = null;
                    }
                    // Since the Parent property can only be set in the default state, we will
                    // set the Parent variable on that instead of the SelectedState.Self.SelectedStateSave
                    var stateToAssignOn = targetElementSave.DefaultState;

                    var oldValue = stateToAssignOn.GetValue(variableName) as string;
                    stateToAssignOn.SetValue(variableName, parentName, "string");
                    SetVariableLogic.Self.PropertyValueChanged("Parent", oldValue);


                }
                else
                {
                    List<InstanceSave> instances = new List<InstanceSave>() { draggedAsInstanceSave };
                    EditingManager.Self.PasteInstanceSaves(instances,
                        draggedAsInstanceSave.ParentContainer.DefaultState.Clone(),
                        targetElementSave);
                }
            }
        }

        private void HandleDroppedItemInWireframe(object draggedObject, out bool handled)
        {
            handled = false;

            if (Cursor.IsInWindow)
            {   
                ElementSave draggedAsElementSave = draggedObject as ElementSave;                    
                ElementSave target = Wireframe.WireframeObjectManager.Self.ElementShowing;

                // Depending on how fast the user clicks the UI may think they dragged an instance rather than 
                // an element, so let's protect against that with this null check.
                if (draggedAsElementSave != null)
                {
                    var newInstance = HandleDroppedElementInElement(draggedAsElementSave, target, out handled);

                    float worldX, worldY;
                    Renderer.Self.Camera.ScreenToWorld(Cursor.X, Cursor.Y,
                                                       out worldX, out worldY);

                    SetInstanceToPosition(worldX, worldY, newInstance);

                    SaveAndRefresh();
                }
            }
        }

        private static InstanceSave HandleDroppedElementInElement(ElementSave draggedAsElementSave, ElementSave target, out bool handled)
        {
            InstanceSave newInstance = null;

            string errorMessage = null;

            handled = false;

            errorMessage = GetErrorMessage(draggedAsElementSave, target, errorMessage);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                MessageBox.Show(errorMessage);
            }
            else
            {
#if DEBUG
                if (draggedAsElementSave == null)
                {
                    throw new Exception("DraggedAsElementSave is null and it shouldn't be.  For vic - try to put this exception earlier to see what's up.");
                }
#endif

                string name = GetUniqueNameForNewInstance(draggedAsElementSave, target);

                // First we want to re-select the target so that it is highlighted in the tree view and not
                // the object we dragged off.  This is so that plugins can properly use the SelectedElement.
                ElementTreeViewManager.Self.Select(target);

                newInstance = ElementTreeViewManager.Self.AddInstance(name, draggedAsElementSave.Name, target);
                handled = true;
            }

            return newInstance;
        }

        private static string GetErrorMessage(ElementSave draggedAsElementSave, ElementSave target, string errorMessage)
        {
            if (target == null)
            {
                errorMessage = "No Screen or Component selected";
            }

            if (errorMessage == null && target is StandardElementSave)
            {
                // do nothing, it's annoying:
                errorMessage = "Standard types can't contain objects";
            }

            if (errorMessage == null && draggedAsElementSave is ScreenSave)
            {
                errorMessage = "Screens can't be dropped into other Screens or Components";
            }

            if (errorMessage == null)
            {

                if (draggedAsElementSave is ComponentSave && target is ComponentSave)
                {
                    ComponentSave targetAsComponentSave = target as ComponentSave;

                    if (!targetAsComponentSave.CanContainInstanceOfType(draggedAsElementSave.Name))
                    {
                        errorMessage = "Can't add instance of " + draggedAsElementSave.Name + " in " + targetAsComponentSave.Name;
                    }
                }
            }


            if (errorMessage == null && target.IsSourceFileMissing)
            {
                errorMessage = "The source file for " + target.Name + " is missing, so it cannot be edited";
            }

            return errorMessage;
        }

        private static string GetUniqueNameForNewInstance(ElementSave elementSave, ElementSave element)
        {
#if DEBUG
            if (elementSave == null)
            {
                throw new ArgumentNullException("elementSave");
            }
#endif
            // remove the path - we dont want folders to be part of the name
            string name = FileManager.RemovePath( elementSave.Name ) + "Instance";
            IEnumerable<string> existingNames = element.Instances.Select(i => i.Name);

            return StringFunctions.MakeStringUnique(name, existingNames);
        }

        private bool CanDrop()
        {
            return SelectedState.Self.SelectedStandardElement == null &&    // Don't allow dropping on standard elements
                   SelectedState.Self.SelectedElement != null &&            // An element must be selected
                   SelectedState.Self.SelectedStateSave != null;            // A state must be selected
        }

        internal void HandleFileDragEnter(object sender, DragEventArgs e)
        {
            if (CanDrop() && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        internal void HandleFileDragDrop(object sender, DragEventArgs e)
        {
            if (!CanDrop())
                return;

            float worldX, worldY;
            Renderer.Self.Camera.ScreenToWorld(Cursor.X, Cursor.Y, out worldX, out worldY);

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // If only one file was dropped, see if we're over an instance that can take a file
            if (files.Length == 1)
            {
                if (!ValidExtension(files[0]))
                    return;

                InstanceSave instance = FindInstanceWithSourceFile(worldX, worldY);
                if (instance != null)
                {
                    string fileName = FileManager.MakeRelative(files[0], FileLocations.Self.ProjectFolder);

                    MultiButtonMessageBox mbmb = new MultiButtonMessageBox();
                    mbmb.StartPosition = FormStartPosition.Manual;
                    
                    mbmb.Location = new Point(MainWindow.MousePosition.X - mbmb.Width / 2,
                         MainWindow.MousePosition.Y - mbmb.Height / 2);

                    mbmb.MessageText = "What do you want to do with the file " + fileName;

                    mbmb.AddButton("Set source file on " + instance.Name, DialogResult.OK);
                    mbmb.AddButton("Add new Sprite", DialogResult.Yes);
                    mbmb.AddButton("Nothing", DialogResult.Cancel);

                    var result = mbmb.ShowDialog();

                    if (result == DialogResult.OK)
                    {
                        SelectedState.Self.SelectedStateSave.SetValue(instance.Name + ".SourceFile", fileName, instance);
                        SaveAndRefresh();
                        return;
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        return;
                    }
                    // continue for DialogResult.Yes
                }
            }

            bool shouldUpdate = false;

            foreach (string file in files)
            {
                if (!ValidExtension(file))
                    continue;

                string fileName = FileManager.MakeRelative(file, FileLocations.Self.ProjectFolder);
                AddNewInstanceForDrop(fileName, worldX, worldY);
                shouldUpdate = true;
            }

            if (shouldUpdate)
                SaveAndRefresh();
        }

        private bool ValidExtension(string file)
        {
            string extension = FileManager.GetExtension(file);
            return LoaderManager.Self.ValidTextureExtensions.Contains(extension);
        }

        private InstanceSave FindInstanceWithSourceFile(float worldX, float worldY)
        {
            List<ElementWithState> elementStack = new List<ElementWithState>();
            elementStack.Add(new ElementWithState(SelectedState.Self.SelectedElement) { StateName = SelectedState.Self.SelectedStateSave.Name });

            IPositionedSizedObject ipsoOver = SelectionManager.Self.GetRepresentationAt(worldX, worldY, false, elementStack);

            if (ipsoOver != null && ipsoOver.Tag is InstanceSave)
            {
                var baseStandardElement = ObjectFinder.Self.GetRootStandardElementSave(ipsoOver.Tag as InstanceSave);

                if (baseStandardElement.DefaultState.Variables.Any(v => v.Name == "SourceFile"))
                {
                    return ipsoOver.Tag as InstanceSave;
                }
            }

            return null;
        }

        private static void SaveAndRefresh()
        {
            GumCommands.Self.FileCommands.TryAutoSaveCurrentElement();
            PropertyGridManager.Self.RefreshUI();
            GumCommands.Self.GuiCommands.RefreshElementTreeView();

            WireframeObjectManager.Self.RefreshAll(true);
        }

        private static void AddNewInstanceForDrop(string fileName, float worldX, float worldY)
        {
            string nameToAdd = FileManager.RemovePath(FileManager.RemoveExtension(fileName));

            IEnumerable<string> existingNames = SelectedState.Self.SelectedElement.Instances.Select(i => i.Name);
            nameToAdd = StringFunctions.MakeStringUnique(nameToAdd, existingNames);

            InstanceSave instance =
                ElementCommands.Self.AddInstance(SelectedState.Self.SelectedElement, nameToAdd);
            instance.BaseType = "Sprite";

            SetInstanceToPosition(worldX, worldY, instance);

            SelectedState.Self.SelectedStateSave.SetValue(instance.Name + ".SourceFile", fileName, instance);
        }

        private static void SetInstanceToPosition(float worldX, float worldY, InstanceSave instance)
        {
            var component = SelectedState.Self.SelectedComponent;

            float xToSet = worldX;
            float yToSet = worldY;

            float containerLeft = 0;
            float containerTop = 0;

            float containerWidth = ProjectState.Self.GumProjectSave.DefaultCanvasWidth;
            float containerHeight = ProjectState.Self.GumProjectSave.DefaultCanvasHeight;

            if (component != null)
            {
                var runtime = Wireframe.WireframeObjectManager.Self.GetRepresentation(component);
                containerLeft = runtime.GetAbsoluteLeft();
                containerTop = runtime.GetAbsoluteTop();

                containerWidth = runtime.Width;
                containerHeight = runtime.Height;
                
            }
            else
            {
                // leave default
            }

            var differenceX = worldX - containerLeft;
            var instanceXUnits = (PositionUnitType)SelectedState.Self.SelectedStateSave.GetValueRecursive($"{instance.Name}.X Units");
            var asGeneralXUnitType = UnitConverter.ConvertToGeneralUnit(instanceXUnits);
            xToSet = UnitConverter.Self.ConvertXPosition(differenceX, GeneralUnitType.PixelsFromSmall, asGeneralXUnitType, containerWidth);

            var differenceY = worldY - containerTop;
            var instanceYUnits = (PositionUnitType)SelectedState.Self.SelectedStateSave.GetValueRecursive($"{instance.Name}.Y Units");
            var asGeneralYUnitType = UnitConverter.ConvertToGeneralUnit(instanceYUnits);
            yToSet = UnitConverter.Self.ConvertYPosition(differenceY, GeneralUnitType.PixelsFromSmall, asGeneralYUnitType, containerHeight);


            SelectedState.Self.SelectedStateSave.SetValue(instance.Name + ".X", xToSet);
            SelectedState.Self.SelectedStateSave.SetValue(instance.Name + ".Y", yToSet);
        }
    }
}
