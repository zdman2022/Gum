﻿using System;
using System.Collections.Generic;
using Gum.DataTypes;
using Gum.Managers;
using System.Windows.Forms;
using Gum.Plugins;
using Gum.Wireframe;
using Gum.Settings;
using ToolsUtilities;
using System.IO;
using CommonFormsAndControls.Forms;
using System.Diagnostics;
using Gum.ToolStates;
using Gum.Logic.FileWatch;
using Gum.CommandLine;
using Gum.DataTypes.Variables;
using System.Linq;

namespace Gum
{
    public class ProjectManager
    {
        #region Fields

        GumProjectSave mGumProjectSave;

        static ProjectManager mSelf;

        bool mHaveErrorsOccurred = false;

        #endregion

        #region Properties

        public static ProjectManager Self
        {
            get
            {
                if (mSelf == null)
                {
                    mSelf = new ProjectManager();
                }
                return mSelf;
            }
        }

        public GumProjectSave GumProjectSave
        {
            get
            {
                return mGumProjectSave;
            }
        }

        public GeneralSettingsFile GeneralSettingsFile
        {
            get;
            private set;
        }

        public bool HaveErrorsOccurred
        {
            get
            {
                return mHaveErrorsOccurred;
            }
        }
        #endregion

        #region Methods


        private ProjectManager()
        {

        }

        public void Initialize()
        {
            GeneralSettingsFile = GeneralSettingsFile.LoadOrCreateNew();

            CommandLineManager.Self.ReadCommandLine();

            if(!CommandLineManager.Self.ShouldExitImmediately)
            {
                var isShift = (Control.ModifierKeys & Keys.Shift) != 0;

                if (!isShift && !string.IsNullOrEmpty(CommandLineManager.Self.GlueProjectToLoad))
                {
                    GumCommands.Self.FileCommands.LoadProject(CommandLineManager.Self.GlueProjectToLoad);

                    if (!string.IsNullOrEmpty(CommandLineManager.Self.ElementName))
                    {
                        SelectedState.Self.SelectedElement = ObjectFinder.Self.GetElementSave(CommandLineManager.Self.ElementName);
                    }
                }
                else if (!isShift && !string.IsNullOrEmpty(GeneralSettingsFile.LastProject))
                {
                    GumCommands.Self.FileCommands.LoadProject(GeneralSettingsFile.LastProject);
                }
                else
                {
                    CreateNewProject();
                }
            }
        }

        public void CreateNewProject()
        {
            FileWatchLogic.Self.HandleProjectUnloaded();

            mGumProjectSave = new GumProjectSave();
            ObjectFinder.Self.GumProjectSave = mGumProjectSave;

            StandardElementsManager.Self.PopulateProjectWithDefaultStandards(mGumProjectSave);
            // Now that a new project is created, refresh the UI!
            GumCommands.Self.GuiCommands.RefreshElementTreeView();

            PluginManager.Self.ProjectLoad(mGumProjectSave);
        }

        public bool LoadProject()
        {
            var openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Gum Project (*.gumx)|*.gumx";
            openFileDialog.Title = "Select project to load";

            DialogResult result = openFileDialog.ShowDialog();

                

            if (result == DialogResult.OK)
            {
                string fileName = openFileDialog.FileName;

                SelectedState.Self.SelectedInstance = null;
                SelectedState.Self.SelectedElement = null;

                GumCommands.Self.FileCommands.LoadProject(fileName);

                return true;
            }


            return false;
        }

        // made public so that File commands can access this function
        public void LoadProject(FilePath fileName)
        {
            GumLoadResult result;

            mGumProjectSave = GumProjectSave.Load(fileName.FullPath, out result);

            string errors = result.ErrorMessage;

            if (!string.IsNullOrEmpty(errors))
            {
                MessageBox.Show("Errors loading " + fileName + "\n\n" + errors);
                
                // If the file doesn't exist, that's okay we will let the user still work - it's not like they can overwrite a file that doesn't exist
                // But if it does exist, we want to be careful and not allow overwriting because they could be wiping out good data
                if (fileName.Exists())
                {
                    mHaveErrorsOccurred = true;
                }

                // We used to not load the project, but maybe we still should, just disable autosaving
                //
                //mGumProjectSave = new GumProjectSave();
            }
            else
            {
                mHaveErrorsOccurred = false;
            }

            ObjectFinder.Self.GumProjectSave = mGumProjectSave;

            WpfDataUi.Controls.FileSelectionDisplay.FolderRelativeTo = fileName.GetDirectoryContainingThis().FullPath;

            if (mGumProjectSave != null)
            {
                bool wasModified = mGumProjectSave.Initialize();

                RecreateMissingStandardElements();

                if(RecreateMissingDefinedByBaseObjects())
                {
                    wasModified = true;
                }

                if(mGumProjectSave.AddNewStandardElementTypes())
                {
                    wasModified = true;
                }
                if(FixSlashesInNames(mGumProjectSave))
                {
                    wasModified = true;
                }
                if(RemoveDuplicateVariables(mGumProjectSave))
                {
                    wasModified = true;
                }

                mGumProjectSave.FixStandardVariables();

                FileManager.RelativeDirectory = fileName.GetDirectoryContainingThis().FullPath;
                mGumProjectSave.RemoveDuplicateVariables();


                GraphicalUiElement.ShowLineRectangles = mGumProjectSave.ShowOutlines;
                EditingManager.Self.RestrictToUnitValues = mGumProjectSave.RestrictToUnitValues;

                CopyLinkedComponents();

                PluginManager.Self.ProjectLoad(mGumProjectSave);

                StandardElementsManager.Self.RefreshStateVariablesThroughPlugins();

                if (wasModified)
                {
                    ProjectManager.Self.SaveProject(forceSaveContainedElements:true);
                }

                GraphicalUiElement.CanvasWidth = mGumProjectSave.DefaultCanvasWidth;
                GraphicalUiElement.CanvasHeight = mGumProjectSave.DefaultCanvasHeight;

                FileWatchLogic.Self.HandleProjectLoaded();
            }
            else
            {
                PluginManager.Self.ProjectLoad(mGumProjectSave);
            }

            // Deselect everything
            SelectedState.Self.SelectedElement = null;
            // Now that a new project is loaded, refresh the UI!
            GumCommands.Self.GuiCommands.RefreshElementTreeView();


            if(mGumProjectSave != null)
            {
                GumCommands.Self.FileCommands.LoadLocalizationFile();
            }

            GeneralSettingsFile.AddToRecentFilesIfNew(fileName);

            var shouldSaveSettings = false;
            if (GeneralSettingsFile.LastProject != fileName)
            {
                GeneralSettingsFile.LastProject = fileName.FullPath;
                shouldSaveSettings = true;
            }

            if(!string.IsNullOrEmpty(result.ErrorMessage))
            {
                if(!fileName.Exists())
                {
                    var numberRemoved = GeneralSettingsFile.RecentProjects.RemoveAll(item => item.FilePath == fileName);
                    if(numberRemoved > 0)
                    {
                        shouldSaveSettings = true;
                    }
                }

            }

            if(shouldSaveSettings)
            {
                GeneralSettingsFile.Save();
            }
        }

        private void CopyLinkedComponents()
        {
            var gumDirectory = new FilePath(mGumProjectSave.FullFileName).GetDirectoryContainingThis();

            void CopyReference(ElementReference reference)
            {
                if (reference.LinkType == LinkType.CopyLocally && !string.IsNullOrEmpty(reference.Link))
                {
                    // copy from the original location here
                    var source = gumDirectory.Original + reference.Link;
                    var destination = gumDirectory.Original + reference.Subfolder + "\\" + reference.Name + "." + reference.Extension;

                    try
                    {
                        System.IO.File.Copy(source, destination, overwrite: true);
                    }
                    catch (Exception e)
                    {
                        GumCommands.Self.GuiCommands.PrintOutput($"Error {e}");
                    }
                }
            }

            foreach(var reference in mGumProjectSave.ScreenReferences)
            {
                CopyReference(reference);
            }

            foreach (var reference in mGumProjectSave.ComponentReferences)
            {
                CopyReference(reference);
            }

            foreach (var reference in mGumProjectSave.StandardElementReferences)
            {
                CopyReference(reference);
            }
        }

        private bool FixSlashesInNames(GumProjectSave mGumProjectSave)
        {
            var toReturn = false;
            foreach(var reference in mGumProjectSave.ComponentReferences)
            {
                if(reference.Name.Contains("\\"))
                {
                    reference.Name = reference.Name.Replace("\\", "/");
                    toReturn = true;
                }
            }

            foreach(var component in mGumProjectSave.Components)
            {
                if(component.Name.Contains("\\"))
                {
                    component.Name = component.Name.Replace("\\", "/");
                    toReturn = true;
                }
            }

            return toReturn;
        }

        private bool RemoveDuplicateVariables(GumProjectSave gumProjectSave)
        {
            var didChange = false;
            foreach(var screen in gumProjectSave.Screens)
            {
                didChange = RemoveDuplicateVariables(screen) || didChange;
            }
            foreach(var component in gumProjectSave.Components)
            {
                didChange = RemoveDuplicateVariables(component) || didChange;
            }
            foreach (var standard in gumProjectSave.StandardElements)
            {
                didChange = RemoveDuplicateVariables(standard) || didChange;
            }
            return didChange;
        }

        private bool RemoveDuplicateVariables(ElementSave element)
        {
            var didChange = false;
            foreach(var state in element.AllStates)
            {
                didChange = RemoveDuplicateVariables(state) || didChange;
            }
            return didChange;
        }

        private bool RemoveDuplicateVariables(StateSave state)
        {
            var variableNames = state.Variables.Select(item => item.Name).ToHashSet();

            var didChange = false;
            if (variableNames.Count != state.Variables.Count)
            { 
                var newVariables = new List<VariableSave>();
                foreach(var variableName in variableNames)
                {
                    var matchingVariable = state.Variables.FirstOrDefault(item => item.Name == variableName);
                    newVariables.Add(matchingVariable);
                }

                state.Variables.Clear();
                state.Variables.AddRange(newVariables);
                didChange = true;
            }
            return didChange;
        }

        private void RecreateMissingStandardElements()
        {
            List<StandardElementSave> missingElements = new List<StandardElementSave>();
            foreach (var element in mGumProjectSave.StandardElements)
            {
                if (element.IsSourceFileMissing)
                {
                    missingElements.Add(element);

                }
            }

            foreach (var element in missingElements)
            {
                var result = MessageBox.Show(
                    "The following standard is missing: " + element.Name + "  Recreate it?", "Recreate " + element.Name + "?", MessageBoxButtons.OKCancel);

                if (result == DialogResult.OK)
                {
                    mGumProjectSave.StandardElements.RemoveAll(item => item.Name == element.Name);
                    mGumProjectSave.StandardElementReferences.RemoveAll(item => item.Name == element.Name);

                    var newElement = StandardElementsManager.Self.AddStandardElementSaveInstance(mGumProjectSave, element.Name);

                    string gumProjectDirectory = FileManager.GetDirectory(mGumProjectSave.FullFileName);

                    mGumProjectSave.SaveStandardElements(gumProjectDirectory);
                }
            }
        }

        private bool RecreateMissingDefinedByBaseObjects()
        {
            var wasAnythingAdded = false;

            foreach(var component in mGumProjectSave.Components)
            {
                List<InstanceSave> necessaryInstances = new List<InstanceSave>();
                FillWithNecessaryInstances(component, necessaryInstances);
                foreach(var instanceInBase in necessaryInstances)
                {
                    // see if there's a match:
                    var matching = component.GetInstance(instanceInBase.Name);

                    if(matching == null)
                    {
                        var instance = instanceInBase.Clone();
                        instance.DefinedByBase = true;
                        component.Instances.Add(instance);
                        wasAnythingAdded = true;
                    }
                }
            }
            return wasAnythingAdded;
        }

        private void FillWithNecessaryInstances(ComponentSave component, List<InstanceSave> necessaryInstances)
        {
            if( !string.IsNullOrWhiteSpace( component.BaseType))
            {
                var baseComponent = ObjectFinder.Self.GetElementSave(component.BaseType) as ComponentSave;

                if(baseComponent != null)
                {
                    necessaryInstances.AddRange(baseComponent.Instances);

                    FillWithNecessaryInstances(baseComponent, necessaryInstances);
                }
            }
        }

        internal void SaveProject(bool forceSaveContainedElements = false)
        {
            bool succeeded = false;

            if (mHaveErrorsOccurred)
            {
                MessageBox.Show("Can't save project because errors occurred when the project was last loaded");
            }
            else
            {
                bool isNewProject;
                bool shouldSave = AskUserForProjectNameIfNecessary(out isNewProject);

                if (shouldSave)
                {
                    PluginManager.Self.BeforeProjectSave(GumProjectSave);

                    bool saveContainedElements = isNewProject || forceSaveContainedElements;
                    
                    try
                    {

                        if (saveContainedElements)
                        {
                            foreach (var screenSave in GumProjectSave.Screens)
                            {
                                PluginManager.Self.BeforeElementSave(screenSave);
                            }
                            foreach (var componentSave in GumProjectSave.Components)
                            {
                                PluginManager.Self.BeforeElementSave(componentSave);
                            }
                            foreach (var standardElementSave in GumProjectSave.StandardElements)
                            {
                                PluginManager.Self.BeforeElementSave(standardElementSave);
                            }
                        }
                        FileWatchLogic.Self.IgnoreNextChangeOn(GumProjectSave.FullFileName);

                        GumCommands.Self.TryMultipleTimes(() => GumProjectSave.Save(GumProjectSave.FullFileName, saveContainedElements));

                        succeeded = true;

                        if (succeeded && saveContainedElements)
                        {
                            foreach (var screenSave in GumProjectSave.Screens)
                            {
                                PluginManager.Self.AfterElementSave(screenSave);
                            }
                            foreach (var componentSave in GumProjectSave.Components)
                            {
                                PluginManager.Self.AfterElementSave(componentSave);
                            }
                            foreach (var standardElementSave in GumProjectSave.StandardElements)
                            {
                                PluginManager.Self.AfterElementSave(standardElementSave);
                            }
                        }
                    }
                    catch(UnauthorizedAccessException exception)
                    {
                        var tempFileName = FileManager.RemoveExtension(GumProjectSave.FullFileName) + DateTime.Now.ToString("s") + "gumx";
                        GumCommands.Self.TryMultipleTimes(() => GumProjectSave.Save(tempFileName, saveContainedElements));

                        string fileName = TryGetFileNameFromException(exception);
                        if (fileName != null && IsFileReadOnly(fileName))
                        {
                            ShowReadOnlyDialog(fileName);
                        }
                        else
                        {
                            MessageBox.Show($"Error trying to save the project, but backup was saved at \n\n{tempFileName}\n\n Additional information:\n\n" + exception.ToString());
                        }
                    }

                    // This may be the first time the file is being saved.  If so, we should make it relative
                    FileManager.RelativeDirectory = FileManager.GetDirectory(GumProjectSave.FullFileName);

                    if (succeeded)
                    {
                        PluginManager.Self.ProjectSave(GumProjectSave);
                    }
                }
            }
        }

        private static string TryGetFileNameFromException(UnauthorizedAccessException exception)
        {
            string message = exception.Message;

            int start = message.IndexOf('\'') + 1;
            int end = message.IndexOf('\'', start);

            if (start != -1 && end != -1)
                return message.Substring(start, end - start);

            return null;
        }

        public string MakeAbsoluteIfNecessary(string textureAsString)
        {
            if (!string.IsNullOrEmpty(textureAsString) && FileManager.IsRelative(textureAsString))
            {
                textureAsString = FileManager.RemoveDotDotSlash(FileManager.RelativeDirectory + textureAsString);
            }
            return textureAsString;
        }

        public bool AskUserForProjectNameIfNecessary(out bool isProjectNew)
        {
            isProjectNew = false;
            bool shouldSave = true;
            // If it's null, that means the user hasn't saved this file yet
            if (string.IsNullOrEmpty(GumProjectSave.FullFileName))
            {
                shouldSave = false;

                var openFileDialog = new SaveFileDialog();

                openFileDialog.Filter = "Gum Project (*.gumx)|*.gumx";
                openFileDialog.Title = "Where would you like to save the Gum project?";

                DialogResult result = openFileDialog.ShowDialog();


                if (result == DialogResult.OK)
                {
                    GumProjectSave.FullFileName = openFileDialog.FileName;
                    shouldSave = true;
                    isProjectNew = true;
                }
            }
            return shouldSave;
        }

        public static void ShowReadOnlyDialog(string fileName)
        {
            MultiButtonMessageBox mbmb = new MultiButtonMessageBox();

            mbmb.StartPosition = FormStartPosition.Manual;

            mbmb.Location = new System.Drawing.Point(MainWindow.MousePosition.X - mbmb.Width / 2,
                 MainWindow.MousePosition.Y - mbmb.Height / 2);

            mbmb.MessageText = "Could not save the file\n\n" + fileName + "\n\nbecause it is read-only." +
                "What would you like to do?";

            mbmb.AddButton("Nothing (file will not save, Gum will continue to work normally)", DialogResult.Cancel);
            mbmb.AddButton("Open folder containing file", DialogResult.OK);

            var dialogResult = mbmb.ShowDialog();

            if (dialogResult == DialogResult.OK)
            {
                // Let's select the file instead of just opening the folder
                //string folder = FileManager.GetDirectory(fileName);
                //Process.Start(folder);
                Process.Start("explorer.exe", "/select," + fileName);
            }
        }

        public static bool IsFileReadOnly(string fileName)
        {
            return System.IO.File.Exists(fileName) && new FileInfo(fileName).IsReadOnly;
        }

        #endregion
    }
}
