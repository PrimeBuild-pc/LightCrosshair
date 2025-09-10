using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;

namespace LightCrosshair
{
    internal sealed class ProfilesDialog : Form
    {
        private readonly IProfileService _service;
        private ListBox _lst = null!;
        private TextBox _txtName = null!;
        private Label _lblHotkey = null!;
        private Button _btnSetHotkey = null!;
        private Button _btnClearHotkey = null!;
        private Button _btnAdd = null!;
        private Button _btnClone = null!;
        private Button _btnDelete = null!;
        private Button _btnClose = null!;
        private Button _btnUp = null!;
        private Button _btnDown = null!;
        private Label _lblStatus = null!;
        private bool _capturingHotkey;
        private bool _suppress;

        public ProfilesDialog(IProfileService service)
        {
            _service = service;
            Text = "Profiles";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(480, 400);
            Font = SystemFonts.MessageBoxFont;
            InitializeUi();
            LoadData();
            _service.CurrentChanged += Service_CurrentChanged;
            KeyPreview = true;
            this.KeyDown += ProfilesDialog_KeyDown;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                var boundsString = Properties.Settings.Default.ProfilesDlgBounds;
                if (!string.IsNullOrWhiteSpace(boundsString))
                {
                    var rect = (Rectangle?)new RectangleConverter().ConvertFromString(boundsString);
                    if (rect.HasValue && rect.Value.Width > 100 && rect.Value.Height > 100) this.Bounds = rect.Value;
                }
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.ProfilesDlgBounds = new RectangleConverter().ConvertToString(this.Bounds) ?? string.Empty;
                Properties.Settings.Default.Save();
            }
            catch { }
            base.OnFormClosed(e);
        }

        private void InitializeUi()
        {
            _lst = new ListBox { Location = new Point(12, 12), Size = new Size(200, 280), IntegralHeight = false, TabIndex = 0, AccessibleName = "Profile list" };
            _lst.SelectedIndexChanged += (_, __) => PopulateFieldsFromSelection();

            var lblName = new Label { Text = "Name", Location = new Point(230, 20), AutoSize = true };
            _txtName = new TextBox { Location = new Point(230, 40), Width = 220, TabIndex = 1, AccessibleName = "Profile name" };
            _txtName.TextChanged += (_, __) => { if (_suppress) return; CommitName(); };

            var lblHotkey = new Label { Text = "Hotkey", Location = new Point(230, 80), AutoSize = true };
            _lblHotkey = new Label { Text = "None", Location = new Point(230, 100), AutoSize = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4), AccessibleName = "Hotkey display" };
            _btnSetHotkey = new Button { Text = "Set", Location = new Point(310, 96), Width = 60, TabIndex = 2, AccessibleName = "Set hotkey" };
            _btnClearHotkey = new Button { Text = "Clear", Location = new Point(380, 96), Width = 60, TabIndex = 3, AccessibleName = "Clear hotkey" };
            _btnSetHotkey.Click += (_, __) => BeginCaptureHotkey();
            _btnClearHotkey.Click += (_, __) => { if (SelectedProfile() is { } p) { UpdateProfileHotkey(p, Keys.None); } };

            _btnAdd = new Button { Text = "Add", Location = new Point(12, 304), Width = 60, TabIndex = 4, AccessibleName = "Add profile" };
            _btnClone = new Button { Text = "Clone", Location = new Point(78, 304), Width = 60, TabIndex = 5, AccessibleName = "Clone profile" };
            _btnDelete = new Button { Text = "Delete", Location = new Point(144, 304), Width = 60, TabIndex = 6, AccessibleName = "Delete profile" };
            _btnUp = new Button { Text = "Up", Location = new Point(12, 334), Width = 60, TabIndex = 7, AccessibleName = "Move profile up" };
            _btnDown = new Button { Text = "Down", Location = new Point(78, 334), Width = 60, TabIndex = 8, AccessibleName = "Move profile down" };
            _btnClose = new Button { Text = "Close", Location = new Point(370, 320), Width = 80, DialogResult = DialogResult.OK, TabIndex = 20, AccessibleName = "Close dialog" };
            _btnAdd.Click += (_, __) => AddNew();
            _btnClone.Click += (_, __) => CloneSelected();
            _btnDelete.Click += (_, __) => DeleteSelected();
            _btnUp.Click += (_, __) => MoveSelected(-1);
            _btnDown.Click += (_, __) => MoveSelected(1);
            _btnClose.Click += (_, __) => Close();

            _lblStatus = new Label { Text = " ", Location = new Point(150, 334), Size = new Size(240, 24), AccessibleName = "Status" };

            var tip = new ToolTip { AutomaticDelay = 150, ReshowDelay = 150, InitialDelay = 150, ShowAlways = true };
            tip.SetToolTip(_btnSetHotkey, "Capture new hotkey (Esc to clear while capturing)");
            tip.SetToolTip(_btnClearHotkey, "Clear hotkey");
            tip.SetToolTip(_btnUp, "Move profile up (Ctrl+Up)");
            tip.SetToolTip(_btnDown, "Move profile down (Ctrl+Down)");
            tip.SetToolTip(_lst, "Profiles (F2 rename, Del delete, Enter apply name)");

            Controls.AddRange(new Control[] { _lst, lblName, _txtName, lblHotkey, _lblHotkey, _btnSetHotkey, _btnClearHotkey,
                _btnAdd, _btnClone, _btnDelete, _btnUp, _btnDown, _btnClose, _lblStatus });
        }

        private void LoadData()
        {
            _suppress = true;
            _lst.Items.Clear();
            foreach (var p in _service.Profiles) _lst.Items.Add(new ListItem(p));
            var cur = _service.Current;
            for (int i = 0; i < _lst.Items.Count; i++)
                if (((ListItem)_lst.Items[i]).Profile.Id == cur.Id) { _lst.SelectedIndex = i; break; }
            PopulateFieldsFromSelection();
            _suppress = false;
        }

        private void PopulateFieldsFromSelection()
        {
            if (_lst.SelectedItem is ListItem li)
            {
                _suppress = true;
                _txtName.Text = li.Profile.Name;
                _lblHotkey.Text = li.Profile.HotKey == Keys.None ? "None" : li.Profile.HotKey.ToString();
                _suppress = false;
                _service.Switch(li.Profile.Id);
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _btnDelete.Enabled = _service.Profiles.Count > 1 && _lst.SelectedItem != null;
            _btnClone.Enabled = _lst.SelectedItem != null;
            _btnUp.Enabled = _lst.SelectedIndex > 0;
            _btnDown.Enabled = _lst.SelectedIndex >= 0 && _lst.SelectedIndex < _lst.Items.Count - 1;
        }

        private CrosshairProfile? SelectedProfile() => (_lst.SelectedItem as ListItem)?.Profile;

        private void CommitName()
        {
            if (_suppress) return;
            if (SelectedProfile() is not { } p) return;
            var name = _txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            if (name == p.Name) return;
            if (_service.Profiles.Any(x => x.Id != p.Id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { _lblStatus.Text = "Name already exists."; return; }
            var clone = p.Clone();
            clone.Name = name;
            _service.Update(clone);
            LoadData();
            _lblStatus.Text = "Renamed.";
        }

        private void AddNew()
        {
            string baseName = "Profile"; int n = 1;
            while (_service.Profiles.Any(p => p.Name.Equals(baseName + n, StringComparison.OrdinalIgnoreCase))) n++;
            var newProfile = _service.AddClone(_service.Current, baseName + n);
            _service.Switch(newProfile.Id);
            LoadData();
            _lblStatus.Text = "Added.";
        }

        private void CloneSelected()
        {
            if (SelectedProfile() is not { } p) return;
            var clone = _service.AddClone(p, p.Name + " Copy");
            _service.Switch(clone.Id);
            LoadData();
            _lblStatus.Text = "Cloned.";
        }

        private void DeleteSelected()
        {
            if (SelectedProfile() is not { } p) return;
            if (_service.Profiles.Count <= 1) return;
            if (MessageBox.Show(this, $"Delete '{p.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                if (_service.Delete(p.Id)) { LoadData(); _lblStatus.Text = "Deleted."; }
            }
        }

        private void BeginCaptureHotkey()
        {
            if (SelectedProfile() == null) return;
            _capturingHotkey = true;
            _lblHotkey.Text = "Press a key (Esc clears)...";
        }

        private void ProfilesDialog_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_capturingHotkey)
            {
                e.Handled = true;
                if (SelectedProfile() is { } p) { UpdateProfileHotkey(p, e.KeyCode == Keys.Escape ? Keys.None : e.KeyCode); }
                return;
            }
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; return; }
            if (e.KeyCode == Keys.F2) { _txtName.Focus(); _txtName.SelectAll(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Enter && _txtName.Focused) { CommitName(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.Up) { MoveSelected(-1); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.Down) { MoveSelected(1); e.Handled = true; return; }
        }

        private void UpdateProfileHotkey(CrosshairProfile p, Keys key)
        {
            _capturingHotkey = false;
            var updated = p.Clone();
            updated.HotKey = key;
            if (key != Keys.None)
            {
                foreach (var other in _service.Profiles.Where(o => o.Id != p.Id && o.HotKey == key).ToList())
                { var oClone = other.Clone(); oClone.HotKey = Keys.None; _service.Update(oClone); }
            }
            _service.Update(updated);
            LoadData();
            _lblStatus.Text = key == Keys.None ? "Hotkey cleared." : "Hotkey set.";
        }

        private void Service_CurrentChanged(object? sender, CrosshairProfile e)
        {
            if (_lst.SelectedItem is ListItem li && li.Profile.Id == e.Id) return;
            for (int i = 0; i < _lst.Items.Count; i++)
                if (((ListItem)_lst.Items[i]).Profile.Id == e.Id) { _lst.SelectedIndex = i; break; }
            UpdateButtons();
        }

        private void MoveSelected(int delta)
        {
            if (_lst.SelectedItem is not ListItem li) return;
            int oldIndex = _lst.SelectedIndex;
            if (_service.Move(li.Profile.Id, delta))
            {
                int newIndex = Math.Max(0, Math.Min(_lst.Items.Count - 1, oldIndex + delta));
                LoadData();
                _lst.SelectedIndex = newIndex;
                _lblStatus.Text = "Reordered.";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _service.CurrentChanged -= Service_CurrentChanged;
            base.Dispose(disposing);
        }

        private sealed class ListItem
        {
            public CrosshairProfile Profile { get; }
            public ListItem(CrosshairProfile p) { Profile = p; }
            public override string ToString() => Profile.HotKey == Keys.None ? Profile.Name : $"{Profile.Name} ({Profile.HotKey})";
        }
    }
}
