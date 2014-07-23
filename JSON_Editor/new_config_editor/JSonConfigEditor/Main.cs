﻿using Aga.Controls.Tree;
using JSonConfigEditor.dataModel;
using JSonConfigEditor.dataModel.configElement;
using JSonConfigEditor.extensions;
using JSonConfigEditor.gui;
using JSonConfigEditor.gui.contextMenu;
using JSonConfigEditor.gui.editor;
using JSonConfigEditor.gui.popup;
using JSonConfigEditor.module;
using JSonConfigEditor.tree;
using JSonConfigEditor.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace JSonConfigEditor.main
{
    public partial class ConfigurationEditor : Form
    {
        #region Public Constants

        // ======================== CONSTANTS ========================
        /// <summary>
        /// The default name given to a newly created node.
        /// </summary>
        public readonly String DEFAULT_NAME = "(untitled)";

        /// <summary>
        /// The name of this program (displayed on the menu bar).
        /// </summary>
        public readonly String PROGRAM_NAME = "Json Configuration Editor " +
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

        #endregion Public Constants

        #region Private Members

        // ======================== PRIVATE MEMBERS ========================
        private readonly ConfigObjectTreeView genericTree = new ConfigObjectTreeView();

        private readonly String META_FILE_WARNING_MSG = "WHAT ARE YOU DOING?!!! YOU SHOULD NOT BE READING THIS FILE. IT IS STRICTLY FORBIDDEN. THERE SHOULD BE NO REASON WHY YOU'RE READING THIS FILE, ABSOLUTELY NONE. ANY CHANGES SHOULD BE DONE VIA THE CONFIG EDITOR. IF YOU FORCIFULLY EDIT THIS FILE, ALL HELL WILL BREAK LOSE.\n\nThank you for your cooperation,\nDevelopers of the Almighty JSON Config Editor\n\n";

        private readonly String TRACK_FILE_WARNING_MSG = "You're just casually reading this file right? YOU'RE NOT PLANNING ON EDITTING IT ARE YOU?! o___O. Please do not edit this file.\n\nThank you for your cooperation,\nDevelopers of the Almighty JSON Config Editor\n\n";

        private readonly ToolTip TOOL_TIP = new ToolTip();

        private readonly State<TreeState> state;

        private readonly FieldEditor fieldEditor;

        private readonly ObjectEditor objectEditor;

        private readonly CollectionEditor collectionEditor;

        private readonly TreeViewContextMenu TREE_CONTEXT_MENU;

        private readonly FileSystemWatcher watcher = new FileSystemWatcher();

        private readonly BackgroundWorker loadBackgroundWorker = new BackgroundWorker();

        private readonly BackgroundWorker stateChangedBackgroundWorker = new BackgroundWorker();

        #endregion Private Members

        #region Properties

        // ======================== PROPERTIES ========================
        public ConfigObjectTreeView GenericTree
        {
            get { return this.genericTree; }
        }

        private String _fileName, _trackFileName, _templateFileName;

        private String FileName
        {
            set
            {
                _fileName = value;
                if (String.IsNullOrEmpty(value))
                {
                    _trackFileName = "";
                    _templateFileName = "";
                }
                else
                {
                    String name = Path.GetDirectoryName(_fileName) + "\\"
                    + Path.GetFileNameWithoutExtension(_fileName);
                    String extension = Path.GetExtension(_fileName);
                    _trackFileName = name + "_prettified" + extension;
                    _templateFileName = name + ".do_not_touch";
                }
            }
            get
            {
                return _fileName;
            }
        }

        private String TrackFileName
        {
            get { return _trackFileName; }
        }

        private String TemplateFileName
        {
            get { return _templateFileName; }
        }

        #endregion Properties

        #region Events

        // ======================== EVENTS ========================
        /**
         * Fired after a file has been successfully saved.
         */

        public event SaveEventHandler AfterSave;

        #endregion Events

        public ConfigurationEditor()
        {
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = Icon.FromHandle(Properties.Resources.config_editor_icon.GetHicon());
            this.genericTree.Root.Data = new ConfigObject();
            this.genericTree.HideSelection = false;

            this.state = new State<TreeState>(PROGRAM_NAME,
                TreeState.Saved, new Dictionary<TreeState, String>
                {
                    {TreeState.Saved, ""},
                    {TreeState.Modified, "*"}
                });
            genericTree.TreeStateChanged += new TreeStateChangeHandler(genericTree_TreeStateChanged);
            InitializeComponent();
            initTreeView();
            initToolTip();

            this.TREE_CONTEXT_MENU = new TreeViewContextMenu(this.genericTree);

            // Leaking "this", but it's ok because we are only storing a reference
            // to it, and using it after the constructor resolves
            this.fieldEditor = new FieldEditor(this);
            this.objectEditor = new ObjectEditor(this);
            this.collectionEditor = new CollectionEditor(this);

            this.splitContainer1.Panel2.Controls.Add(fieldEditor);
            this.splitContainer1.Panel2.Controls.Add(objectEditor);
            this.splitContainer1.Panel2.Controls.Add(collectionEditor);

            this.fieldEditor.Visible = false;
            this.objectEditor.Visible = false;
            this.collectionEditor.Visible = false;

            // Need to initialize menu AFTER initializing all the editors
            initMenu();

            this.SETTINGS_WINDOW = new SettingsWindow(this);
            //initSampleTree();

            /* Watch for changes in LastAccess and LastWrite times, and
            the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*.*";

            // Add event handlers.
            watcher.Changed += watcher_Changed;
            watcher.Deleted += watcher_Changed;

            loadBackgroundWorker.DoWork += loadBackgroundWorker_DoWork;
        }

        private void loadBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Console.WriteLine("Background Worker: " + Thread.CurrentThread.GetHashCode());
            load(false);
        }

        private void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!watcher.EnableRaisingEvents)
            {
                return;
            }
            if (!e.FullPath.Equals(FileName) && !e.FullPath.Equals(TemplateFileName))
            {
                return;
            }
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Changed:
                    watcher.EnableRaisingEvents = false;
                    if (MessageBox.Show(String.Format("{0} has been changed externally. Would you like to reload the file? NOTE: your current changes will be discarded.", e.FullPath),
                        "File Changed Externally", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.Yes)
                    {
                        Console.WriteLine("Watcher: " + Thread.CurrentThread.GetHashCode());
                        this.Invoke((MethodInvoker)delegate
                        {
                            load(false);
                        });
                    }
                    watcher.EnableRaisingEvents = true;
                    break;

                case WatcherChangeTypes.Deleted:
                    watcher.EnableRaisingEvents = false;
                    MessageBox.Show(String.Format("{0} has been deleted externally. If you save, this program will regenerate the file for you.", e.FullPath),
                        "File Deleted Externally", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    watcher.EnableRaisingEvents = true;
                    break;
            }
        }

        /// <summary>
        /// Sets the readonly flag of the given file.
        /// </summary>
        /// <param name="path">the path to the file</param>
        /// <param name="readOnly">true if the readonly only flag should be true, false if the readonly flag should be false</param>
        private void setReadOnly(String path, Boolean readOnly)
        {
            if (!File.Exists(path))
            {
                return;
            }

            FileAttributes attributes = File.GetAttributes(path);
            if (readOnly)
            {
                File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
            }
            else
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        private void genericTree_TreeStateChanged(object sender, TreeStateChangeArg arg)
        {
            this.state.CurrentState = arg.NewState;
            this.Text = this.state.Text;
        }

        #region Constructor Helper Methods

        /**
         * ======================== CONSTRUCTOR HELPER METHODS ========================
         *
         * Most properties of the menu controls are set through the Visual Studio
         * property window. The ones that cannot be set through the property window
         * are set here.
         */

        private void initTreeView()
        {
            this.splitContainer1.Panel1.Controls.Add(this.genericTree);
            this.genericTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.genericTree.Font = new System.Drawing.Font("Microsoft Sans Serif",
                12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point,
                ((byte)(0)));
            this.genericTree.Location = new System.Drawing.Point(0, 0);
            this.genericTree.Name = "treeView1";
            this.genericTree.Size = new System.Drawing.Size(311, 364);
            this.genericTree.TabIndex = 0;
            this.genericTree.TabStop = false;
            //this.genericTree.LabelEdit = true;
            this.genericTree.SelectionChanged += new EventHandler(genericTree_SelectionChanged);
        }

        private void initToolTip()
        {
            //TOOL_TIP.InitialDelay = 1000;
            //TOOL_TIP.ShowAlways = true;

            //TOOL_TIP.SetToolTip(this.buttonAddObject, "Add a child to the selected node");
        }

        private void initMenu()
        {
            this.menuFileExit.ShortcutKeyDisplayString = "Esc";

            this.menuEditModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.menuEditModeComboBox.SelectedIndex = 0;
            /*
            this.menuViewCollapse.ShortcutKeyDisplayString = "-";
            this.menuViewExpand.ShortcutKeyDisplayString = "+";

            this.menuViewCollapse.ToolTipText = "Collapse the selected node and all its children";
            this.menuViewExpand.ToolTipText = "Expand the selected node and all its children";
        */
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        #endregion Constructor Helper Methods

        #region Event Helper Methods (IO Methods)

        // ======================== EVENT HELPER METHODS ========================

        private void exit()
        {
            this.Close();
        }

        private Boolean save()
        {
            // Show the SaveFileDialog if there is no FileName
            if (String.IsNullOrEmpty(this.FileName))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "JSON File (*.json)|*.json";
                saveFileDialog.Title = "Save";
                saveFileDialog.AddExtension = true;

                saveFileDialog.ShowDialog();

                if (String.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return false;
                }

                this.FileName = saveFileDialog.FileName;
            }

            watcher.EnableRaisingEvents = false;

            setReadOnly(this.FileName, false);
            String value = JsonConvert.SerializeObject(this.genericTree.getSerializableValueObject());
            if (!saveString(this.FileName, value))
            {
                watcher.EnableRaisingEvents = true;
                return false;
            }
            setReadOnly(this.FileName, true);

            setReadOnly(this.TrackFileName, false);
            if (!saveString(this.TrackFileName, TRACK_FILE_WARNING_MSG + JsonUtil.FormatJson(value)))
            {
                watcher.EnableRaisingEvents = true;
                return false;
            }
            setReadOnly(this.TrackFileName, true);

            String template = JsonConvert.SerializeObject(this.genericTree.getSerializableTemplateObject());
            setReadOnly(this.TemplateFileName, false);
            if (!saveString(this.TemplateFileName, META_FILE_WARNING_MSG + JsonUtil.FormatJson(template)))
            {
                watcher.EnableRaisingEvents = true;
                return false;
            }
            setReadOnly(this.TemplateFileName, true);

            if (AfterSave != null)
            {
                AfterSave(this, new SaveEventArgs());
            }
            watcher.EnableRaisingEvents = true;
            return true;
        }

        private Boolean saveString(String fileName, String content)
        {
            try
            {
                System.IO.File.WriteAllText(fileName, content);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    String.Format("An exception was thrown while saving the file: {0}\n{1}", fileName, ex.Message));
                return false;
            }
        }

        private String loadAsString(String fileName)
        {
            try
            {
                String content = System.IO.File.ReadAllText(fileName);

                return content.Substring(content.IndexOf('{'));
            }
            catch (Exception ex)
            {
                MessageBox.Show("An exception was thrown while loading the file: "
                    + fileName + "\n" + ex.Message);
                return null;
            }
        }

        private Boolean load()
        {
            return load(true);
        }

        private Boolean load(Boolean showDialog)
        {
            Console.WriteLine("Load: " + Thread.CurrentThread.GetHashCode());
            if (showDialog)
            {
                OpenFileDialog loadFileDialog = new OpenFileDialog();
                loadFileDialog.Title = "Open a Json Config File";
                loadFileDialog.ShowDialog();

                if (String.IsNullOrEmpty(loadFileDialog.FileName))
                {
                    return false;
                }
                this.FileName = loadFileDialog.FileName;
            }

            if (System.IO.File.Exists(this.TemplateFileName))
            {
                String template = loadAsString(this.TemplateFileName);
                if (String.IsNullOrEmpty(template))
                {
                    return false;
                }
                JToken token = JsonUtil.deserialize(template);
                if (token == null)
                {
                    return false;
                }
                this.genericTree.loadFromTemplate(token);
            }
            else if (System.IO.File.Exists(this.FileName))
            {
                String json = loadAsString(this.FileName);
                if (String.IsNullOrEmpty(json))
                {
                    return false;
                }
                JToken token = JsonUtil.deserialize(json);
                if (token == null)
                {
                    return false;
                }
                this.genericTree.loadFromJson(token);
            }
            else
            {
                MessageBox.Show("Cannot find file : {0}", this.FileName);
                return false;
            }

            // Make the FileSystemWatcher "watch" a different file
            watcher.Path = Path.GetDirectoryName(FileName);
            watcher.EnableRaisingEvents = true;
            initTreeView();

            // Change the String displayed on top of the program to include the name of the file being edited.
            this.state.BaseText = PROGRAM_NAME + ": " + Path.GetFullPath(this.FileName).TruncateFromFront(40, true);
            genericTree.State = TreeState.Saved;
            return true;
        }

        #endregion Event Helper Methods (IO Methods)

        #region Event Handlers

        // ======================== EVENT HANDLERS ========================

        #region Menu Handlers

        /**
         * ======================== MENU HANDLERS ========================
         */
        /**
         * Save the data to a Json file.
         */

        private void menuSave_Click(object sender, EventArgs e)
        {
            if (save())
            {
                genericTree.State = TreeState.Saved;
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.FileName = String.Empty;
            if (save())
            {
                genericTree.State = TreeState.Saved;
            }
        }

        private void menuFileExit_Click(object sender, EventArgs e)
        {
            exit();
        }

        private void menuEditModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            EditorMode mode = (EditorMode)this.menuEditModeComboBox.SelectedIndex;
            this.fieldEditor.Mode = mode;
            this.objectEditor.Mode = mode;
            this.collectionEditor.Mode = mode;
            this.TREE_CONTEXT_MENU.Mode = mode;
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.genericTree.ActionManager.undo())
            {
                MessageBox.Show("No more action to undo");
            }
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.genericTree.ActionManager.redo())
            {
                MessageBox.Show("No more action to redo");
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (genericTree.State == TreeState.Modified)
            {
                // Ask the user if they want to save if the tree has been modified
                switch (MessageBox.Show("Save changes to the current tree before loading a new one?",
                    "Confirm save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        // Save the tree
                        if (save())
                        {
                            genericTree.State = TreeState.Saved;

                            // Only load after the save is successful
                            load();
                        }
                        break;

                    case DialogResult.No:
                        // Do not save the tree and continue with load
                        load();
                        break;

                    case DialogResult.Cancel:
                        // Do not save the tree, but aborts load (i.e. do nothing)
                        break;
                }
            }
            else
            {
                load();
            }
        }

        private readonly SettingsWindow SETTINGS_WINDOW;

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.SETTINGS_WINDOW.ShowDialog();
        }

        #endregion Menu Handlers

        #region TreeView Handlers

        // ======================== TREEVIEW HANDLERS ========================
        /// <summary>
        /// After a tree node is selected, populate the textboxes with the information
        /// from the selected node for editing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void genericTree_SelectionChanged(object sender, EventArgs e)
        {
            if (!genericTree.hasSelectedNode())
            {
                return;
            }

            GenericNode<Element> selectedNode = genericTree.getSelectedNode();

            Utils.doIfType(selectedNode.Data,
                (ConfigObject obj) =>
                {
                    this.fieldEditor.Visible = false;
                    this.objectEditor.Visible = true;
                    this.collectionEditor.Visible = false;
                    objectEditor.setData(selectedNode, obj);
                },
                (ConfigCollection collectionNode) =>
                {
                    this.fieldEditor.Visible = false;
                    this.objectEditor.Visible = false;
                    this.collectionEditor.Visible = true;
                    collectionEditor.setData(selectedNode, collectionNode);
                },
                (ConfigField fieldNode) =>
                {
                    this.fieldEditor.Visible = true;
                    this.objectEditor.Visible = false;
                    this.collectionEditor.Visible = false;
                    fieldEditor.setData(selectedNode, fieldNode);
                },
                (Object other) =>
                {
                    this.fieldEditor.Visible = false;
                    this.objectEditor.Visible = false;
                    this.collectionEditor.Visible = false;
                });
        }

        #endregion TreeView Handlers

        // ======================== MISC HANDLERS ========================
        private void windowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (genericTree.State == TreeState.Modified)
            {
                // Ask the user if they want to save if the tree has been modified
                switch (MessageBox.Show("Save changes before exit?", "Confirm exit",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        // Save the tree
                        // If the saving was not successful, abort exit
                        // If the saving was successful, continue with exit
                        e.Cancel = !save();
                        break;

                    case DialogResult.No:
                        // Do not save the tree and continue with exit
                        break;

                    case DialogResult.Cancel:
                        // Do not save the tree, but aborts exit
                        e.Cancel = true;
                        break;
                }
            }
            else
            {
                // If no changes were made, ask the user if they want to exit
                //e.Cancel = (MessageBox.Show("Are you sure you want to exit?", "Confirm exit",
                //MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No);
            }
        }

        #endregion Event Handlers

        private readonly SearchWindow SEARCH_WINDOW = new SearchWindow();

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SEARCH_WINDOW.setTreeView(genericTree);
            SEARCH_WINDOW.setMainWindow(this);
            SEARCH_WINDOW.StartPosition = FormStartPosition.CenterScreen;
            SEARCH_WINDOW.Show();
        }
    }

    #region Event Delegates

    // ======================== EVENT DELEGATES ========================
    public delegate void SaveEventHandler(object source, SaveEventArgs args);

    public class SaveEventArgs : EventArgs
    {
    }

    #endregion Event Delegates
}