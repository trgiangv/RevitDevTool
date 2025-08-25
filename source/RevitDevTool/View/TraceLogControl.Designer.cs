using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox;

namespace RevitDevTool.View
{
    partial class TraceLogControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                
                // Clean up our custom resources
                DisposeCustomResources();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _mainPanel = new TableLayoutPanel();
            _topPanel = new TableLayoutPanel();
            _logLevelComboBox = new ComboBox();
            _buttonPanel = new FlowLayoutPanel();
            _clearLogButton = new Button();
            _clearGeometryButton = new Button();
            _startStopToggle = new CheckBox();
            _logPanel = new System.Windows.Forms.Panel();
            _logTextBox = new RichTextBox();
            _mainPanel.SuspendLayout();
            _topPanel.SuspendLayout();
            _buttonPanel.SuspendLayout();
            _logPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _mainPanel
            // 
            _mainPanel.ColumnCount = 1;
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _mainPanel.Controls.Add(_topPanel, 0, 0);
            _mainPanel.Controls.Add(_logPanel, 0, 1);
            _mainPanel.Dock = DockStyle.Fill;
            _mainPanel.Location = new System.Drawing.Point(0, 0);
            _mainPanel.Margin = new Padding(0);
            _mainPanel.Name = "_mainPanel";
            _mainPanel.Padding = new Padding(8);
            _mainPanel.RowCount = 2;
            _mainPanel.RowStyles.Add(new RowStyle());
            _mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _mainPanel.Size = new Size(600, 400);
            _mainPanel.TabIndex = 0;
            // 
            // _topPanel
            // 
            _topPanel.AutoSize = true;
            _topPanel.ColumnCount = 2;
            _topPanel.ColumnStyles.Add(new ColumnStyle());
            _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _topPanel.Controls.Add(_logLevelComboBox, 0, 0);
            _topPanel.Controls.Add(_buttonPanel, 1, 0);
            _topPanel.Dock = DockStyle.Fill;
            _topPanel.Location = new System.Drawing.Point(8, 8);
            _topPanel.Margin = new Padding(0, 0, 0, 8);
            _topPanel.Name = "_topPanel";
            _topPanel.RowCount = 1;
            _topPanel.RowStyles.Add(new RowStyle());
            _topPanel.Size = new Size(584, 38);
            _topPanel.TabIndex = 0;
            // 
            // _logLevelComboBox
            // 
            _logLevelComboBox.Anchor = AnchorStyles.Left;
            _logLevelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _logLevelComboBox.FlatStyle = FlatStyle.System;
            _logLevelComboBox.Font = new Font("Segoe UI", 9F);
            _logLevelComboBox.FormattingEnabled = true;
            _logLevelComboBox.Location = new System.Drawing.Point(0, 7);
            _logLevelComboBox.Margin = new Padding(0, 0, 12, 0);
            _logLevelComboBox.MinimumSize = new Size(120, 0);
            _logLevelComboBox.Name = "_logLevelComboBox";
            _logLevelComboBox.Size = new Size(120, 23);
            _logLevelComboBox.TabIndex = 0;
            // 
            // _buttonPanel
            // 
            _buttonPanel.Anchor = AnchorStyles.Right;
            _buttonPanel.AutoSize = true;
            _buttonPanel.Controls.Add(_clearLogButton);
            _buttonPanel.Controls.Add(_clearGeometryButton);
            _buttonPanel.Controls.Add(_startStopToggle);
            _buttonPanel.Location = new System.Drawing.Point(198, 0);
            _buttonPanel.Margin = new Padding(0);
            _buttonPanel.Name = "_buttonPanel";
            _buttonPanel.Size = new Size(386, 38);
            _buttonPanel.TabIndex = 1;
            _buttonPanel.WrapContents = false;
            // 
            // _clearLogButton
            // 
            _clearLogButton.AutoSize = true;
            _clearLogButton.Cursor = Cursors.Hand;
            _clearLogButton.Font = new Font("Segoe UI", 9F);
            _clearLogButton.Location = new System.Drawing.Point(0, 3);
            _clearLogButton.Margin = new Padding(0, 3, 8, 3);
            _clearLogButton.MinimumSize = new Size(100, 32);
            _clearLogButton.Name = "_clearLogButton";
            _clearLogButton.Size = new Size(100, 32);
            _clearLogButton.TabIndex = 0;
            _clearLogButton.Text = "🗑️ Clear Log";
            _clearLogButton.UseVisualStyleBackColor = true;
            // 
            // _clearGeometryButton
            // 
            _clearGeometryButton.AutoSize = true;
            _clearGeometryButton.Cursor = Cursors.Hand;
            _clearGeometryButton.Font = new Font("Segoe UI", 9F);
            _clearGeometryButton.Location = new System.Drawing.Point(108, 3);
            _clearGeometryButton.Margin = new Padding(0, 3, 8, 3);
            _clearGeometryButton.MinimumSize = new Size(130, 32);
            _clearGeometryButton.Name = "_clearGeometryButton";
            _clearGeometryButton.Size = new Size(130, 32);
            _clearGeometryButton.TabIndex = 1;
            _clearGeometryButton.Text = "🔷 Clear Geometry";
            _clearGeometryButton.UseVisualStyleBackColor = true;
            // 
            // _startStopToggle
            // 
            _startStopToggle.Appearance = Appearance.Button;
            _startStopToggle.AutoSize = true;
            _startStopToggle.Checked = true;
            _startStopToggle.CheckState = CheckState.Checked;
            _startStopToggle.Cursor = Cursors.Hand;
            _startStopToggle.Font = new Font("Segoe UI Emoji", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            _startStopToggle.Location = new System.Drawing.Point(246, 3);
            _startStopToggle.Margin = new Padding(0, 3, 0, 3);
            _startStopToggle.MinimumSize = new Size(140, 32);
            _startStopToggle.Name = "_startStopToggle";
            _startStopToggle.Size = new Size(140, 32);
            _startStopToggle.TabIndex = 2;
            _startStopToggle.Text = "⏸️ Stop Listener";
            _startStopToggle.TextAlign = ContentAlignment.MiddleCenter;
            _startStopToggle.UseVisualStyleBackColor = true;
            // 
            // _logPanel
            // 
            _logPanel.BorderStyle = BorderStyle.FixedSingle;
            _logPanel.Controls.Add(_logTextBox);
            _logPanel.Dock = DockStyle.Fill;
            _logPanel.Location = new System.Drawing.Point(8, 54);
            _logPanel.Margin = new Padding(0);
            _logPanel.Name = "_logPanel";
            _logPanel.Padding = new Padding(3);
            _logPanel.Size = new Size(584, 338);
            _logPanel.TabIndex = 1;
            // 
            // _logTextBox
            // 
            _logTextBox.BackColor = SystemColors.Window;
            _logTextBox.BorderStyle = BorderStyle.None;
            _logTextBox.Dock = DockStyle.Fill;
            _logTextBox.Font = new Font("Cascadia Mono", 9F);
            _logTextBox.HideSelection = false;
            _logTextBox.Location = new System.Drawing.Point(3, 3);
            _logTextBox.Margin = new Padding(0);
            _logTextBox.Name = "_logTextBox";
            _logTextBox.ReadOnly = true;
            _logTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _logTextBox.Size = new Size(576, 330);
            _logTextBox.TabIndex = 0;
            _logTextBox.Text = "";
            // 
            // TraceLogControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            Controls.Add(_mainPanel);
            Font = new Font("Segoe UI", 9F);
            Margin = new Padding(0);
            MinimumSize = new Size(400, 200);
            Name = "TraceLogControl";
            Size = new Size(600, 400);
            _mainPanel.ResumeLayout(false);
            _mainPanel.PerformLayout();
            _topPanel.ResumeLayout(false);
            _topPanel.PerformLayout();
            _buttonPanel.ResumeLayout(false);
            _buttonPanel.PerformLayout();
            _logPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
    }
}
