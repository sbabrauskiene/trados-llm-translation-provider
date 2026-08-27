using System;
using System.Drawing;
using System.Windows.Forms;
using Trados.LlmTranslationProvider.Security;

namespace Trados.LlmTranslationProvider.UI
{
    /// <summary>
    /// Settings dialog for one instance of the LLM translation provider. Hand-built in code
    /// (rather than the usual Designer.cs split) since there is no Windows Forms Designer
    /// available in this development setup - functionally identical once compiled.
    /// </summary>
    public class LlmTranslationOptionsForm : Form
    {
        private TextBox _apiKeyTextBox;
        private TextBox _modelTextBox;
        private TextBox _termbasePathTextBox;
        private TextBox _promptTemplatePathTextBox;
        private CheckBox _useTmCheckBox;
        private Button _okButton;
        private Button _cancelButton;

        public LlmTranslationOptions Options { get; private set; }

        public LlmTranslationOptionsForm(LlmTranslationOptions options)
        {
            Options = options ?? new LlmTranslationOptions();
            BuildLayout();
            LoadFromOptions();
        }

        private void BuildLayout()
        {
            Text = "LLM Translation Provider Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 260);
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

            AddLabel(layout, "OpenAI API key:", 0);
            _apiKeyTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            layout.Controls.Add(_apiKeyTextBox, 1, 0);
            layout.SetColumnSpan(_apiKeyTextBox, 2);

            AddLabel(layout, "Model:", 1);
            _modelTextBox = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(_modelTextBox, 1, 1);
            layout.SetColumnSpan(_modelTextBox, 2);

            AddLabel(layout, "Termbase (TBX):", 2);
            _termbasePathTextBox = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(_termbasePathTextBox, 1, 2);
            var browseTermsButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            browseTermsButton.Click += (s, e) => BrowseForFile(_termbasePathTextBox, "TBX termbase files (*.tbx)|*.tbx|All files (*.*)|*.*");
            layout.Controls.Add(browseTermsButton, 2, 2);

            AddLabel(layout, "Prompt template:", 3);
            _promptTemplatePathTextBox = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(_promptTemplatePathTextBox, 1, 3);
            var browsePromptButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            browsePromptButton.Click += (s, e) => BrowseForFile(_promptTemplatePathTextBox, "Text files (*.txt)|*.txt|All files (*.*)|*.*");
            layout.Controls.Add(browsePromptButton, 2, 3);

            _useTmCheckBox = new CheckBox { Text = "Include TM matches as examples in the prompt (when available)", AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.Add(_useTmCheckBox, 1, 4);
            layout.SetColumnSpan(_useTmCheckBox, 2);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            _okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            _okButton.Click += OkButton_Click;
            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_okButton);
            layout.Controls.Add(buttonPanel, 1, 5);
            layout.SetColumnSpan(buttonPanel, 2);

            Controls.Add(layout);
            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private static void AddLabel(TableLayoutPanel layout, string text, int row)
        {
            layout.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        }

        private static void BrowseForFile(TextBox target, string filter)
        {
            using (var dialog = new OpenFileDialog { Filter = filter })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    target.Text = dialog.FileName;
                }
            }
        }

        private void LoadFromOptions()
        {
            _apiKeyTextBox.Text = ApiKeyStore.Load(ApiKeyStore.OpenAiKeyId) ?? string.Empty;
            _modelTextBox.Text = Options.Model;
            _termbasePathTextBox.Text = Options.TermbasePath;
            _promptTemplatePathTextBox.Text = Options.PromptTemplatePath;
            _useTmCheckBox.Checked = Options.UseTranslationMemoryContext;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_modelTextBox.Text))
            {
                MessageBox.Show(this, "Please specify a model name (e.g. gpt-4.1).", "Model required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            // The API key is persisted separately via DPAPI - never through LlmTranslationOptions
            // (see its class remarks), so it never ends up in the provider URI or project file.
            ApiKeyStore.Save(ApiKeyStore.OpenAiKeyId, _apiKeyTextBox.Text);

            Options = new LlmTranslationOptions
            {
                Model = _modelTextBox.Text.Trim(),
                TermbasePath = _termbasePathTextBox.Text.Trim(),
                PromptTemplatePath = _promptTemplatePathTextBox.Text.Trim(),
                UseTranslationMemoryContext = _useTmCheckBox.Checked
            };
        }
    }
}
