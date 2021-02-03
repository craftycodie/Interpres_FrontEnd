﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Interpreter.IO;
using System.IO;
using Interpreter;
using System.Threading;
using Interpreter.Tokenizers;
using Interpres_FrontEnd.Commands;

namespace Interpres
{
    public partial class MainForm : Form
    {
        private List<LocalFileWorkspace> openWorkspaces = new List<LocalFileWorkspace>();

        public LocalFileWorkspace FocusedWorkspace { 
            get { return this.tabControl1.SelectedIndex >= 0 ? openWorkspaces.ElementAt(this.tabControl1.SelectedIndex) : null; }  
            set
            {
                textEditorBox.Enabled = value != null;
                fontToolStripMenuItem.Enabled = value != null;
                saveAsToolStripMenu.Enabled = value != null;
                saveToolStripMenu.Enabled = value != null && value.Path != null;
                closeToolStripMenu.Enabled = value != null;
                findAndReplaceToolStripMenuItem.Enabled = value != null;
                editToolStripMenuItem.Enabled = value != null;
                dataMenuItem.Enabled = value != null;
                commandInputBox.Enabled = value != null;
                commandExecuteButton.Enabled = value != null;

                tabControl1.SelectedIndex = openWorkspaces.IndexOf(value);
                TextBoxLines = value != null ? value.script : new string[0];
                commandInputBox.Text = value != null ? value.command : "";

                UpdateVariableList();
                UpdateScrollback();
                filePathLabel.Text = value != null? value.Path : "No file loaded.";
                //listBox1.Items.AddRange(value.variables);
            }
        }

        private string ArrayToString(object[] array)
        {
            string[] valueStrings = new string[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                valueStrings[i] = array[i].ToString();
                if (array[i].GetType().IsArray)
                    valueStrings[i] = ArrayToString((object[])array[i]);
            }
            return "[" + string.Join(", ", valueStrings) + "]";
        }

        public void UpdateVariableList()
        {
            listBox1.Items.Clear();
            if (FocusedWorkspace != null)
            {
                foreach (KeyValuePair<string, object> variablePair in FocusedWorkspace.variables)
                {
                    string valueString = variablePair.Value.ToString();

                    if (variablePair.Value.GetType().IsArray)
                    {
                        valueString = ArrayToString((object[])variablePair.Value);
                    }

                    listBox1.Items.Add(variablePair.Key + ": " + valueString);
                }
            }
        }

        public void UpdateScrollback()
        {
            richTextBox1.Text = "";
            if (FocusedWorkspace != null)
                richTextBox1.AppendText(string.Join("\r\n", FocusedWorkspace.commandLog));
        }

        //A property to access textEditorBox.Lines publically.
        public string[] TextBoxLines { get { return this.textEditorBox.Lines; } set {this.textEditorBox.Lines = value; } }

        public MainForm()
        {
            InitializeComponent();
            UpdateOptions();
        }

        private void UpdateOptions()
        {
            if (lineNumbersToolStripMenuItem.Checked)
                lineNumbersBox.Show();
            else
                lineNumbersBox.Hide();

            if (statusBarToolStripMenuItem.Checked)
                textStatusStrip.Show();
            else
                textStatusStrip.Hide();
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                openWorkspaces.Add(new LocalFileService().OpenWorkspace() as LocalFileWorkspace);
                tabControl1.TabPages.Add(Path.GetFileName(openWorkspaces.Last().Path));
                FocusedWorkspace = openWorkspaces.Last();
            } catch (IOException ex)
            {
                // ignore.
            }
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new LocalFileService().SaveWorkspace(FocusedWorkspace);
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FocusedWorkspace.SaveWorkspace();
        }

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int focusedIndex = openWorkspaces.IndexOf(FocusedWorkspace);
            tabControl1.TabPages.RemoveAt(focusedIndex);
            openWorkspaces.RemoveAt(focusedIndex);
            FocusedWorkspace = openWorkspaces.Count > 0 ? openWorkspaces.ElementAt(Math.Max(focusedIndex - 1, 0)) : null;

        }


        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openWorkspaces.Add(new LocalFileWorkspace(null, new string[0]));
            tabControl1.TabPages.Add("New File");
            FocusedWorkspace = openWorkspaces.Last();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            printDialog.ShowDialog();
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox about = new AboutBox();
            about.ShowDialog();
        }

        private void FontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fontDialog.Font = textEditorBox.Font;
            fontDialog.ShowDialog();
            textEditorBox.Font = fontDialog.Font;
        }

        private void LineNumbersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateOptions();
        }

        private void TextEditorBox_TextChanged(object sender, EventArgs e)
        {
            FocusedWorkspace.script = textEditorBox.Lines;

            List<string> lineNumbers = new List<string>();
            for (int i = 1; i < textEditorBox.Lines.Length + 1; i++)
            {
                lineNumbers.Add(i.ToString());
            }
            lineNumbersBox.Lines = lineNumbers.ToArray();

            this.linesCountLabel.Text = this.textEditorBox.Lines.Count().ToString() + " Lines";
            this.characterCountLabel.Text = this.textEditorBox.Text.Length.ToString() + " Characters";
            int wordCount = this.textEditorBox.Text.Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count();
            this.wordCountLabel.Text = wordCount.ToString() + " Words";
            string[] paragraphs = this.textEditorBox.Text.Split(new string[] { "\r\r", "\n\n", "\r\n\r\n", "\n\r\n\r" }, StringSplitOptions.RemoveEmptyEntries);
            this.paragraphCountLabel.Text = paragraphs.Count().ToString() + " Paragraphs";
        }

        private void FindAndReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FindAndReplaceForm findAndReplaceForm = new FindAndReplaceForm();
            findAndReplaceForm.ShowDialog();

            FindAndReplaceAll(findAndReplaceForm.Find, findAndReplaceForm.Replace);
            TextEditorBox_TextChanged(null, EventArgs.Empty);
        }

        public void FindAndReplaceAll(string find, string replace)
        {
            if (string.IsNullOrWhiteSpace(find))
                return;

            if (replace == null)
                replace = "";

            string[] lines = textEditorBox.Lines;

            for (int i = 0; i < lines.Count(); i++)
            {
                if(lines[i].Contains(find))
                {
                    lines[i] = lines[i].Replace(find, replace);
                }
            }

            textEditorBox.Lines = lines;
        }

        private void StatusBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateOptions();
        }

        #region Edit Control Events

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.textEditorBox.Cut();
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.textEditorBox.Copy();
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.textEditorBox.Paste();
        }

        private void UndoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.textEditorBox.Undo();
        }

        private void RedoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.textEditorBox.Redo();
        }

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            #if DEBUG
            foreach (Control control in this.Controls)
            {
                control.Visible = true;
            }
#endif

            NewToolStripMenuItem_Click(null, null);
        }

        //This event opens the Help document when the user clicks Help.
        private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("help.html");
        }

        [DllImport("user32.dll")]
        static extern int SetScrollPos(IntPtr handle, int nBar, int nPos, bool bRedraw);
        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        static extern bool PostMessage(IntPtr handle, uint msg, int wParam, int lParam);
        [DllImport("User32.dll")]
        private extern static int GetScrollPos(IntPtr handle, int nBar);

        private void TextEditorBox_VScroll(object sender, EventArgs e)
        {
            int verticalPosition = GetScrollPos(textEditorBox.Handle, 1);
            SetScrollPos(lineNumbersBox.Handle, 0x1, verticalPosition, true);
            PostMessage(lineNumbersBox.Handle, 0x115, 4 + 0x10000 * verticalPosition, 0);
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            AboutBox about = new AboutBox();
            about.ShowDialog();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex < 0 || tabControl1.SelectedIndex > openWorkspaces.Count - 1)
                return;

            FocusedWorkspace = openWorkspaces.ElementAt(tabControl1.SelectedIndex);
        }

        private void ExecuteCommand(string input)
        {
            ExecuteCommand(FocusedWorkspace, input);
        }

        private void ExecuteCommand(Workspace workspace, string input)
        {
            try
            {
                CommandTokenizer commandTokenizer = new CommandTokenizer();
                commandTokenizer.RegisterCommand(new PlotCommand());
                TokenizerService tokenizerService = new TokenizerService(commandTokenizer);
                var tokens = tokenizerService.GetTokens(input);
                workspace.commandLog.AddLast(">> " + input);
                object answer = new AbstractSyntaxTree(tokens.Select(token => (object)token).ToList(), workspace).GetValue();
                if (answer == null)
                    answer = "";
                string answerString = answer.ToString();
                if (answer.GetType().IsArray)
                    answerString = ArrayToString((object[])answer);
                workspace.commandLog.AddLast("ans: " + answerString);
            }
            catch (Exception ex)
            {
                workspace.commandLog.AddLast("err: " + ex.Message);
            }
            UpdateScrollback();
            UpdateVariableList();
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                string input = commandInputBox.Text;
                ExecuteCommand(input);
                commandInputBox.Text = "";
            }
        }

        private void runScriptMenuItem_Click(object sender, EventArgs e)
        {
            Workspace focused = FocusedWorkspace;
            focused.commandLog.AddLast("** Running Script **");
            string scriptText = string.Join("", FocusedWorkspace.script);

            if (scriptText.EndsWith(';')) 
                scriptText = scriptText.Substring(0, scriptText.Length - 1);

            focused.loading = true;

            //Thread scriptThread = new Thread(new ThreadStart(() =>
            //{
                foreach (string command in scriptText.Split(";"))
                {
                    ExecuteCommand(focused, command);
                }
            //}));

            //scriptThread.Start();

            focused.loading = false;
        }

        private void commandExecuteButton_Click(object sender, EventArgs e)
        {
            string input = commandInputBox.Text;
            ExecuteCommand(input);
            commandInputBox.Text = "";
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            FocusedWorkspace.command = commandInputBox.Text;
        }

        private void importJSONMenuItem_Click(object sender, EventArgs e)
        {
            JSONDataImporter jsonDataImporter = new JSONDataImporter();
            foreach (KeyValuePair<string, object> pair in jsonDataImporter.ImportData())
            {
                FocusedWorkspace.variables[pair.Key] = pair.Value;
            }
            UpdateVariableList();
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                FocusedWorkspace.variables.Remove(listBox1.SelectedItem.ToString().Split(':')[0]);
                UpdateVariableList();
            }
        }
    }
}
