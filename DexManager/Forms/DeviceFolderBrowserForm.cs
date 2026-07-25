using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal sealed class DeviceFolderBrowserForm : Form
    {
        private readonly DeviceFolderService _folderService;
        private readonly string _serial;
        private readonly Label _pathLabel;
        private readonly Label _statusLabel;
        private readonly ListBox _folderList;
        private readonly Button _upButton;
        private readonly Button _refreshButton;
        private readonly Button _selectButton;
        private string _currentFolder;
        private bool _loading;

        public DeviceFolderBrowserForm(
            AdbService adbService,
            string serial,
            string initialFolder)
        {
            _folderService = new DeviceFolderService(adbService);
            _serial = serial;
            _currentFolder = DeviceFolderService.NormalizeDisplayPath(
                initialFolder);

            var theme = ThemeColors.Current;
            Text = LocalizationService.Get("DeviceFolder.Title");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(610, 470);
            Font = UiFonts.Create(9.5F);
            BackColor = theme.WindowBackground;

            var header = new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(14F, FontStyle.Bold),
                ForeColor = theme.TextPrimary,
                BackColor = theme.WindowBackground,
                Location = new Point(18, 16),
                Text = LocalizationService.Get("DeviceFolder.Heading")
            };
            _pathLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = theme.TextSecondary,
                BackColor = theme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(18, 55),
                Size = new Size(574, 34),
                Padding = new Padding(8, 8, 8, 0)
            };
            _folderList = new ListBox
            {
                Location = new Point(18, 99),
                Size = new Size(574, 292),
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = theme.CardBackground,
                ForeColor = theme.TextPrimary,
                Font = UiFonts.Create(10F)
            };
            _folderList.DoubleClick += delegate
            {
                var item = _folderList.SelectedItem as FolderItem;
                if (item != null) Navigate(item.Path);
            };
            _folderList.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                var item = _folderList.SelectedItem as FolderItem;
                if (item == null) return;
                e.Handled = true;
                Navigate(item.Path);
            };

            _statusLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = theme.TextTertiary,
                BackColor = theme.WindowBackground,
                Location = new Point(18, 398),
                Size = new Size(300, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _upButton = CreateButton(
                LocalizationService.Get("DeviceFolder.Up"),
                new Point(18, 430),
                90);
            _upButton.Click += delegate
            {
                Navigate(DeviceFolderService.GetParent(_currentFolder));
            };
            _refreshButton = CreateButton(
                LocalizationService.Get("DeviceFolder.Refresh"),
                new Point(116, 430),
                90);
            _refreshButton.Click += delegate { Navigate(_currentFolder); };

            var cancelButton = CreateButton(
                LocalizationService.Get("Common.Cancel"),
                new Point(394, 430),
                94);
            cancelButton.DialogResult = DialogResult.Cancel;
            _selectButton = CreateButton(
                LocalizationService.Get("DeviceFolder.SelectCurrent"),
                new Point(496, 430),
                96);
            _selectButton.Click += delegate
            {
                SelectedPath = _currentFolder;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(header);
            Controls.Add(_pathLabel);
            Controls.Add(_folderList);
            Controls.Add(_statusLabel);
            Controls.Add(_upButton);
            Controls.Add(_refreshButton);
            Controls.Add(cancelButton);
            Controls.Add(_selectButton);
            CancelButton = cancelButton;
            Shown += delegate { Navigate(_currentFolder); };
        }

        public string SelectedPath { get; private set; }

        private async void Navigate(string folder)
        {
            if (_loading) return;
            var requested = DeviceFolderService.NormalizeDisplayPath(folder);
            SetLoading(true);
            try
            {
                var folders = await Task.Run(
                    () => _folderService.ListFolders(_serial, requested));
                if (IsDisposed) return;
                _currentFolder = requested;
                _pathLabel.Text = _currentFolder;
                _folderList.BeginUpdate();
                try
                {
                    _folderList.Items.Clear();
                    foreach (var path in folders)
                        _folderList.Items.Add(new FolderItem(path));
                }
                finally
                {
                    _folderList.EndUpdate();
                }
                _statusLabel.Text = folders.Count == 0
                    ? LocalizationService.Get("DeviceFolder.Empty")
                    : LocalizationService.Format(
                        "DeviceFolder.Count",
                        folders.Count);
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _statusLabel.Text = LocalizationService.Format(
                    "DeviceFolder.LoadFailed",
                    ex.Message);
                _statusLabel.ForeColor = Color.Firebrick;
            }
            finally
            {
                if (!IsDisposed) SetLoading(false);
            }
        }

        private void SetLoading(bool loading)
        {
            _loading = loading;
            _folderList.Enabled = !loading;
            _upButton.Enabled = !loading;
            _refreshButton.Enabled = !loading;
            _selectButton.Enabled = !loading;
            if (loading)
            {
                _pathLabel.Text = DeviceFolderService.NormalizeDisplayPath(
                    _currentFolder);
                _statusLabel.ForeColor = ThemeColors.Current.TextTertiary;
                _statusLabel.Text = LocalizationService.Get(
                    "DeviceFolder.Loading");
            }
        }

        private Button CreateButton(string text, Point location, int width)
        {
            return new ThemedButton
            {
                Text = text,
                Location = location,
                Size = new Size(width, 32),
                BackColor = ThemeColors.Current.WindowBackground,
                ForeColor = ThemeColors.Current.TextSecondary
            };
        }

        private sealed class FolderItem
        {
            public FolderItem(string path)
            {
                Path = path;
            }

            public string Path { get; private set; }

            public override string ToString()
            {
                return DeviceFolderService.GetName(Path);
            }
        }
    }
}
