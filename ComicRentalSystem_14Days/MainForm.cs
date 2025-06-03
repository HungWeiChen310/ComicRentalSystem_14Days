using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComicRentalSystem_14Days.Controls;
using ComicRentalSystem_14Days.Forms;
using ComicRentalSystem_14Days.Interfaces;
using ComicRentalSystem_14Days.Models;
using ComicRentalSystem_14Days.Services;

namespace ComicRentalSystem_14Days
{
    public partial class MainForm : BaseForm
    {
        private readonly User _currentUser;
        private readonly ComicService _comicService;
        private readonly MemberService _memberService;
        private readonly IReloadService _reloadService;
        private readonly ILogger _logger;

        private List<AdminComicStatusViewModel>? _allAdminComicStatuses;
        private string _currentSortColumnName = string.Empty;
        private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;

        private Button? _currentSelectedNavButton;
        private AdminDashboardUserControl? _adminDashboardControl;

        public MainForm() : base()
        {
            InitializeComponent();
            // Ensure null-forgiving operator for non-nullable fields if they are initialized by DI/constructor
            _currentUser = null!;
            _comicService = null!;
            _memberService = null!;
            _reloadService = null!;
            _logger = null!;
        }

        public MainForm(
            ILogger logger,
            ComicService comicService,
            MemberService memberService,
            IReloadService reloadService,
            User currentUser
        ) : base()
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._comicService = comicService ?? throw new ArgumentNullException(nameof(comicService));
            this._memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
            this._reloadService = reloadService ?? throw new ArgumentNullException(nameof(reloadService));
            this._currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

            base.SetLogger(logger);
            InitializeComponent();
            UpdateStatusBar();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                this._logger?.Log(Constants.LoggingMessages.MainFormLoading);
                InitializeModernStyles();
                InitializeAdminDashboardControl();
                SetupUIAccessControls();
                InitializeMainDataGridView();
                SubscribeToControlEvents();
                InitializeFilterControlsAndStyles();
                await LoadInitialDataAsync();
                this._logger?.Log(Constants.LoggingMessages.MainFormLoaded);
            }
            catch (Exception ex)
            {
                _logger?.LogError("MainForm_Load 期間發生未預期的錯誤。", ex);
                MessageBox.Show($"應用程式載入期間發生嚴重錯誤: {ex.Message}\n應用程式可能無法正常運作。", Constants.UI.MessageBoxTitles.LoadError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeModernStyles()
        {
            this._logger?.Log("正在初始化新潮風格...");
            if (this.menuStrip2 != null)
            {
                this.menuStrip2.BackColor = ModernBaseForm.SecondaryColor;
                this.menuStrip2.ForeColor = ModernBaseForm.TextColor;
                this.menuStrip2.Font = ModernBaseForm.PrimaryFontBold ?? new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
                foreach (ToolStripMenuItem item in this.menuStrip2.Items.OfType<ToolStripMenuItem>())
                {
                    item.Font = ModernBaseForm.PrimaryFontBold ?? new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
                    item.ForeColor = ModernBaseForm.TextColor;
                }
            }
            if (this.statusStrip1 != null)
            {
                this.statusStrip1.BackColor = ModernBaseForm.SecondaryColor;
                this.statusStrip1.Font = ModernBaseForm.PrimaryFont ?? new System.Drawing.Font("Segoe UI", 9F);
                if (this.toolStripStatusLabelUser != null)
                    this.toolStripStatusLabelUser.ForeColor = ModernBaseForm.TextColor;
            }
        }

        private void InitializeAdminDashboardControl()
        {
            if (_currentUser.Role == UserRole.Admin && _comicService != null && _memberService != null && _logger != null)
            {
                this._logger?.Log("正在為管理員初始化儀表板控制項...");
                if (mainContentPanel != null) mainContentPanel.Controls.Clear();

                _adminDashboardControl = new AdminDashboardUserControl(_comicService, _memberService, _logger)
                {
                    Dock = DockStyle.Fill,
                    Visible = false
                };
                if (mainContentPanel != null) mainContentPanel.Controls.Add(_adminDashboardControl);
            }
        }

        private void SubscribeToControlEvents()
        {
            this._logger?.Log("正在訂閱控制項事件...");
            if (btnNavDashboard != null) btnNavDashboard.Click += btnNavDashboard_Click;
            if (btnNavComicMgmt != null) btnNavComicMgmt.Click += btnNavComicMgmt_Click;
            if (btnNavMemberMgmt != null) btnNavMemberMgmt.Click += btnNavMemberMgmt_Click;
            if (btnNavRentalMgmt != null) btnNavRentalMgmt.Click += btnNavRentalMgmt_Click;
            if (btnNavUserReg != null) btnNavUserReg.Click += btnNavUserReg_Click;
            if (btnNavLogs != null) btnNavLogs.Click += btnNavLogs_Click;

            if (this._comicService != null)
                this._comicService.ComicsChanged += ComicService_ComicsChanged;

            if (this.dgvAvailableComics != null)
            {
                this.dgvAvailableComics.SelectionChanged += dgvAvailableComics_SelectionChanged;
                if (_currentUser.Role == UserRole.Admin)
                {
                    this.dgvAvailableComics.ColumnHeaderMouseClick += dgvAvailableComics_ColumnHeaderMouseClick;
                    this.dgvAvailableComics.CellFormatting += dgvAvailableComics_AdminView_CellFormatting;
                }
            }

            if (this.cmbAdminComicFilterStatus != null && _currentUser.Role == UserRole.Admin)
            {
                this.cmbAdminComicFilterStatus.SelectedIndexChanged += cmbAdminComicFilterStatus_SelectedIndexChanged;
            }

            if (txtSearchAvailableComics != null)
                txtSearchAvailableComics.TextChanged += (s, ev) => { if (IsMemberViewActive()) ApplyAvailableComicsFilter(); };

            if (cmbGenreFilter != null)
                cmbGenreFilter.SelectedIndexChanged += (s, ev) => { if (IsMemberViewActive()) ApplyAvailableComicsFilter(); };

            if (dgvMyRentedComics != null)
            {
                dgvMyRentedComics.CellFormatting += dgvMyRentedComics_CellFormatting;
            }

            if (memberViewTabControl != null)
            {
                memberViewTabControl.SelectedIndexChanged += memberViewTabControl_SelectedIndexChanged;
            }
        }

        private void InitializeMainDataGridView()
        {
            this._logger?.Log("正在初始化主 DataGridView...");
            SetupDataGridView();
        }

        private async Task LoadInitialDataAsync()
        {
            this._logger?.Log("正在載入初始資料...");
            if (_currentUser.Role == UserRole.Admin)
            {
                await LoadAllComicsStatusForAdminAsync();
            }
            else
            {
                 _logger?.Log("[TARGETED_RUNTIME] Applying visibility settings for availableComicsTabPage and dgvAvailableComics in LoadInitialDataAsync for Member.");
                if (_comicService != null) await _comicService.ReloadAsync();
                _logger.Log($"LoadInitialDataAsync (Member) [After ReloadAsync]: _comicService reports {_comicService?.GetAllComics().Count ?? 0} comics.");
                LoadAvailableComics();
                LoadMyRentedComics();
            }
            if (this.dgvAvailableComics != null && this.dgvAvailableComics.Rows.Count > 0)
                 dgvAvailableComics_SelectionChanged(this, EventArgs.Empty);
        }

        private void InitializeFilterControlsAndStyles()
        {
            this._logger?.Log("正在初始化篩選器控制項和樣式...");
            if (this.cmbAdminComicFilterStatus != null && _currentUser.Role == UserRole.Admin)
            {
                this.cmbAdminComicFilterStatus.Items.Clear();
                this.cmbAdminComicFilterStatus.Items.Add(Constants.UI.FilterOptions.All);
                this.cmbAdminComicFilterStatus.Items.Add(Constants.UI.FilterOptions.Rented);
                this.cmbAdminComicFilterStatus.Items.Add(Constants.UI.FilterOptions.Available);
                this.cmbAdminComicFilterStatus.SelectedItem = Constants.UI.FilterOptions.All;
                this.cmbAdminComicFilterStatus.Font = ModernBaseForm.PrimaryFont ?? new System.Drawing.Font("Segoe UI", 9F);
            }

            if (cmbGenreFilter != null && _comicService != null)
            {
                cmbGenreFilter.Items.Clear();
                cmbGenreFilter.Items.Add(Constants.UI.FilterOptions.AllGenres);
                try
                {
                    var genres = _comicService.GetAllComics()
                        .Select(c => c.Genre)
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Distinct()
                        .OrderBy(g => g);
                    foreach (var genre in genres)
                        cmbGenreFilter.Items.Add(genre);
                }
                catch (Exception ex)
                {
                    _logger?.LogError("填入類型篩選器失敗。", ex);
                }
                if (cmbGenreFilter.Items.Count > 0) cmbGenreFilter.SelectedIndex = 0;
                cmbGenreFilter.Font = ModernBaseForm.PrimaryFont ?? new System.Drawing.Font("Segoe UI", 9F);
            }

            SetupSearchBoxPlaceholders();

            if (_currentUser.Role == UserRole.Member)
            {
                if (btnRentComic != null) StyleModernButton(btnRentComic);
                if (dgvAvailableComics != null) StyleModernDataGridView(dgvAvailableComics);
                if (dgvMyRentedComics != null) StyleModernDataGridView(dgvMyRentedComics);
            }

            if (memberViewTabControl != null)
            {
                foreach (TabPage page in memberViewTabControl.TabPages)
                    page.BackColor = ModernBaseForm.SecondaryColor;
            }
        }

        private void SetupSearchBoxPlaceholders()
        {
            this._logger?.Log("正在設定搜尋框預留位置文字...");
            if (txtSearchAvailableComics != null)
            {
                txtSearchAvailableComics.GotFocus += (s, ev) =>
                {
                    if (txtSearchAvailableComics.Text == Constants.UI.Placeholders.SearchComicsPlaceholder)
                    {
                        txtSearchAvailableComics.Text = "";
                        txtSearchAvailableComics.ForeColor = ModernBaseForm.TextColor;
                    }
                };
                txtSearchAvailableComics.LostFocus += (s, ev) =>
                {
                    if (string.IsNullOrWhiteSpace(txtSearchAvailableComics.Text))
                    {
                        txtSearchAvailableComics.Text = Constants.UI.Placeholders.SearchComicsPlaceholder;
                        txtSearchAvailableComics.ForeColor = System.Drawing.Color.Gray;
                    }
                };
                txtSearchAvailableComics.Font = ModernBaseForm.PrimaryFont ?? new System.Drawing.Font("Segoe UI", 9F);
                txtSearchAvailableComics.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                if (string.IsNullOrWhiteSpace(txtSearchAvailableComics.Text))
                {
                     txtSearchAvailableComics.Text = Constants.UI.Placeholders.SearchComicsPlaceholder;
                     txtSearchAvailableComics.ForeColor = System.Drawing.Color.Gray;
                }
            }
        }

        private void dgvAvailableComics_SelectionChanged(object? sender, EventArgs e)
        {
            if (_currentUser is null) return;
            bool isMember = _currentUser.Role == UserRole.Member;
            if (isMember)
            {
                if (btnRentComic is not null && dgvAvailableComics is not null)
                    btnRentComic.Enabled = dgvAvailableComics.SelectedRows.Count > 0;
            }
            else
            {
                if (btnRentComic is not null)
                    btnRentComic.Enabled = false;
            }
        }

        private async void ComicService_ComicsChanged(object? sender, EventArgs e)
        {
            try
            {
                this._logger?.Log(Constants.LoggingMessages.ComicsChangedEventReceived);
                if (_currentUser.Role == UserRole.Admin)
                {
                    this._logger?.Log("正在為管理員重新載入所有漫畫狀態。");
                    await LoadAllComicsStatusForAdminAsync();
                }
                else
                {
                    this._logger?.Log("正在重新載入可借閱漫畫和會員已租借的漫畫。");
                    LoadAvailableComics();
                    LoadMyRentedComics();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("處理 ComicsChanged 事件時發生未預期的錯誤。", ex);
            }
        }

        private void SetupDataGridView()
        {
            if (dgvAvailableComics is null)
            {
                _logger?.LogError("設定DataGridView失敗：dgvAvailableComics 為空。");
                return;
            }

            this._logger?.Log("正在為漫畫設定 DataGridView。");
            dgvAvailableComics.AutoGenerateColumns = false;
            dgvAvailableComics.Columns.Clear();
            dgvAvailableComics.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (_currentUser.Role == UserRole.Admin)
            {
                _logger.Log("正在為管理員視圖設定 DataGridView (所有漫畫狀態)。");
                dgvAvailableComics!.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Title, HeaderText = Constants.ColumnHeaders.Title, FillWeight = 20 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Author, HeaderText = Constants.ColumnHeaders.Author, FillWeight = 15 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Status, HeaderText = Constants.ColumnHeaders.Status, FillWeight = 10 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.BorrowerName, HeaderText = Constants.ColumnHeaders.BorrowerName, FillWeight = 15 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.BorrowerPhoneNumber, HeaderText = Constants.ColumnHeaders.BorrowerPhoneNumber, FillWeight = 15 });
                var rentalDateColumn = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = Constants.DataPropertyNames.RentalDate,
                    HeaderText = Constants.ColumnHeaders.AdminRentalDate,
                    FillWeight = 12,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
                };
                dgvAvailableComics.Columns.Add(rentalDateColumn);
                var returnDateColumn = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = Constants.DataPropertyNames.ReturnDate,
                    HeaderText = Constants.ColumnHeaders.AdminExpectedReturnDate,
                    FillWeight = 13,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
                };
                dgvAvailableComics.Columns.Add(returnDateColumn);
                StyleModernDataGridView(dgvAvailableComics);
            }
            else
            {
                _logger.Log("正在為會員視圖設定 DataGridView (可借閱漫畫)。");
                dgvAvailableComics!.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Title, HeaderText = Constants.ColumnHeaders.Title, FillWeight = 40 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Author, HeaderText = Constants.ColumnHeaders.Author, FillWeight = 30 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Genre, HeaderText = Constants.ColumnHeaders.Genre, FillWeight = 20 });
                dgvAvailableComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Isbn, HeaderText = Constants.ColumnHeaders.ISBN, FillWeight = 30 });
            }

            dgvAvailableComics.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvAvailableComics.MultiSelect = false;
            dgvAvailableComics.ReadOnly = true;
            dgvAvailableComics.AllowUserToAddRows = false;
        }

        private void LoadAvailableComics()
        {
            if (dgvAvailableComics is null || _comicService is null) return;
            this._logger?.Log("正在將可借閱漫畫載入到主表單的 DataGridView。");
            try
            {
                var availableComics = _comicService.GetAllComics().Where(c => !c.IsRented).ToList();
                Action updateGrid = () => {
                    dgvAvailableComics.DataSource = null;
                    dgvAvailableComics.DataSource = availableComics;
                };
                if (dgvAvailableComics.IsHandleCreated && this.InvokeRequired) this.Invoke(updateGrid);
                else if (dgvAvailableComics.IsHandleCreated) updateGrid();
                this._logger?.Log($"已成功載入 {availableComics.Count} 本可借閱漫畫。");
            }
            catch (Exception ex)
            {
                LogErrorActivity("載入可借閱漫畫時發生錯誤。", ex);
                MessageBox.Show($"載入可用漫畫列表時發生錯誤: {ex.Message}", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadAllComicsStatusForAdminAsync()
        {
            if (dgvAvailableComics is null || _memberService is null || _comicService is null) return;
            _logger?.Log("正在非同步為管理員視圖載入所有漫畫狀態。");
            try
            {
                List<Member> allMembers = await Task.Run(() => _memberService.GetAllMembers());
                List<AdminComicStatusViewModel> comicStatuses = await Task.Run(() => _comicService.GetAdminComicStatusViewModels(allMembers));
                _allAdminComicStatuses = new List<AdminComicStatusViewModel>(comicStatuses);
                ApplyAdminComicsView();
                _logger?.Log($"已成功非同步為管理員視圖載入 {comicStatuses.Count} 本漫畫。");
            }
            catch (Exception ex)
            {
                LogErrorActivity("非同步載入所有漫畫狀態供管理員檢視時發生錯誤。", ex);
                Action showError = () => MessageBox.Show($"載入所有漫畫狀態時發生錯誤: {ex.Message}", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (this.IsHandleCreated && !this.IsDisposed) { if (this.InvokeRequired) this.Invoke(showError); else showError(); }
            }
        }

        private void SetupUIAccessControls()
        {
            bool isAdmin = _currentUser.Role == UserRole.Admin;
            _logger.Log($"正在設定UI控制項。使用者是否為管理員: {isAdmin}");

            if (mainContentPanel is null) { _logger?.LogError("設定UI控制項存取權限失敗：mainContentPanel 為空。"); return; }

            if (leftNavPanel is not null) leftNavPanel.Visible = isAdmin;
            if (memberViewTabControl is not null) memberViewTabControl.Visible = !isAdmin;

            // Admin specific panel visibility is handled by SelectNavButton
            if (_adminDashboardControl is not null && !isAdmin) _adminDashboardControl.Visible = false;

            // Visibility of these controls for Admin is handled by SelectNavButton (ShowAdminComicManagementView)
            if (lblAvailableComics is not null && !isAdmin) lblAvailableComics.Visible = true; // visible in member view
            if (cmbAdminComicFilterStatus is not null) cmbAdminComicFilterStatus.Visible = isAdmin; // Only for admin
            if (dgvAvailableComics is not null) dgvAvailableComics.Visible = true; // Visible for both, content changes

            if (this.menuStrip2 is not null)
            {
                var managementMenuItem = this.menuStrip2.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Name == "管理ToolStripMenuItem");
                if (managementMenuItem is not null) managementMenuItem.Visible = isAdmin;
                var toolsMenuItem = this.menuStrip2.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Name == "工具ToolStripMenuItem");
                if (toolsMenuItem is not null) toolsMenuItem.Visible = isAdmin;
                var userRegItem = this.menuStrip2.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Name == "使用者註冊ToolStripMenuItem");
                if (userRegItem is not null) userRegItem.Visible = isAdmin;
            }
            else { _logger.LogWarning("找不到 MenuStrip 控制項 'menuStrip2' 或其為空。"); }

            if (isAdmin)
            {
                this.Text = Constants.UI.FormTitles.AdminBaseTitle + Constants.UI.FormTitles.Dashboard;
                if (btnNavDashboard is not null) SelectNavButton(btnNavDashboard);
                else if (btnNavComicMgmt is not null) SelectNavButton(btnNavComicMgmt);
            }
            else // Member
            {
                this.Text = Constants.UI.FormTitles.MemberBaseTitle;
                if (lblAvailableComics is not null) lblAvailableComics.Text = "請選擇下方漫畫進行租借：";
                if (lblMyRentedComicsHeader is not null) lblMyRentedComicsHeader.Text = "您目前租借的項目：";
                if (btnRentComic is not null) btnRentComic.Visible = true;

                if (memberViewTabControl is not null && memberViewTabControl.TabPages.Count > 0)
                {
                    if (memberViewTabControl.SelectedTab == availableComicsTabPage) ApplyAvailableComicsFilter();
                    else if (memberViewTabControl.SelectedTab == myRentalsTabPage) LoadMyRentedComics();
                }
            }
            _logger.Log($"UI 控制項的可見性和文字已根據管理員狀態 ({isAdmin}) 更新。");
        }

        private void UpdateStatusBar()
        {
            if (this.statusStrip1 != null)
            {
                if (this.toolStripStatusLabelUser != null)
                {
                    this.toolStripStatusLabelUser.Text = $"使用者: {_currentUser.Username} | 角色: {_currentUser.Role}";
                    this._logger.Log($"Status bar updated: User: {_currentUser.Username}, Role: {_currentUser.Role}");
                }
                else { this._logger.LogWarning("找不到 ToolStripStatusLabel 控制項 'toolStripStatusLabelUser' 或其為空。"); }
            }
            else { this._logger.LogWarning("找不到 StatusStrip 控制項 'statusStrip1' 或其為空。"); }
        }

        private void SetupMyRentedComicsDataGridView()
        {
            if (dgvMyRentedComics is null) return;
            _logger?.Log("正在為會員已租借的漫畫設定 DataGridView (dgvMyRentedComics)。");
            dgvMyRentedComics.AutoGenerateColumns = false;
            dgvMyRentedComics.Columns.Clear();
            dgvMyRentedComics.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgvMyRentedComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.ComicTitle, HeaderText = Constants.ColumnHeaders.Title, FillWeight = 30 });
            dgvMyRentedComics.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.Author, HeaderText = Constants.ColumnHeaders.Author, FillWeight = 20 });
            var rentalDateColumn = new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.RentalDate, HeaderText = Constants.ColumnHeaders.MemberRentalDate, FillWeight = 18 };
            rentalDateColumn.DefaultCellStyle ??= new DataGridViewCellStyle();
            rentalDateColumn.DefaultCellStyle.Format = "yyyy-MM-dd";
            dgvMyRentedComics.Columns.Add(rentalDateColumn);
            var returnDateColumn = new DataGridViewTextBoxColumn { DataPropertyName = Constants.DataPropertyNames.ExpectedReturnDate, HeaderText = Constants.ColumnHeaders.MemberExpectedReturnDate, FillWeight = 18 };
            returnDateColumn.DefaultCellStyle ??= new DataGridViewCellStyle();
            returnDateColumn.DefaultCellStyle.Format = "yyyy-MM-dd";
            dgvMyRentedComics.Columns.Add(returnDateColumn);
            if (!dgvMyRentedComics.Columns.Contains("statusColumn"))
            {
                var statusColumn = new DataGridViewTextBoxColumn { Name = "statusColumn", HeaderText = Constants.ColumnHeaders.MemberStatus, FillWeight = 14 };
                dgvMyRentedComics.Columns.Add(statusColumn);
            }
            dgvMyRentedComics.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMyRentedComics.MultiSelect = false;
            dgvMyRentedComics.ReadOnly = true;
            dgvMyRentedComics.AllowUserToAddRows = false;
        }

        private void LoadMyRentedComics()
        {
            if (dgvMyRentedComics is null || _currentUser is null || _comicService is null || _memberService is null || _logger is null)
            { _logger?.LogWarning("載入我的租借漫畫失敗：CurrentUser 或關鍵服務為空。正在清除 DGV。"); ClearDgvMyRentedComics(); return; }
            if (_currentUser.Role != UserRole.Member)
            { _logger?.Log("載入我的租借漫畫失敗：使用者不是會員。正在清除 DGV。"); ClearDgvMyRentedComics(); return; }

            _logger?.LogInformation($"載入我的租借漫畫：正在嘗試為使用者載入租借記錄：'{_currentUser?.Username ?? "未知使用者"}'。");
            try
            {
                Member? currentMember = _memberService.GetMemberByUsername(_currentUser.Username);
                if (currentMember is null)
                { _logger?.LogWarning($"載入我的租借漫畫失敗：找不到使用者名稱 '{_currentUser?.Username ?? "未知使用者"}' 的會員資料。未載入任何租借記錄。"); ClearDgvMyRentedComics(); return; }

                var allComics = _comicService.GetAllComics();
                if (allComics is null) { _logger?.LogWarning("載入我的租借漫畫失敗：_comicService.GetAllComics() 回傳為空。"); ClearDgvMyRentedComics(); return; }

                var myRentedComics = allComics.Where(c => c.IsRented && c.RentedToMemberId == currentMember.Id)
                    .Select(c => new RentalDetailViewModel { ComicId = c.Id, ComicTitle = c.Title, Author = c.Author, RentalDate = c.RentalDate, ExpectedReturnDate = c.ReturnDate }).ToList();
                _logger?.LogInformation($"載入我的租借漫畫：找到會員 ID {currentMember.Id} 的 {myRentedComics.Count} 本已租借漫畫。");

                Action updateGrid = () => { dgvMyRentedComics.DataSource = null; dgvMyRentedComics.DataSource = myRentedComics; };
                if (dgvMyRentedComics.IsHandleCreated && this.InvokeRequired) this.Invoke(updateGrid);
                else if (dgvMyRentedComics.IsHandleCreated) updateGrid();
            }
            catch (Exception ex)
            {
                _logger?.LogError("載入會員已租借漫畫時發生錯誤。", ex);
                MessageBox.Show($"載入您的租借漫畫列表時發生錯誤: {ex.Message}", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ClearDgvMyRentedComics();
            }
        }

        private void ClearDgvMyRentedComics()
        {
            if (dgvMyRentedComics is null) return;
            Action clearGrid = () => dgvMyRentedComics.DataSource = null;
            if (dgvMyRentedComics.IsHandleCreated && this.InvokeRequired) this.Invoke(clearGrid);
            else if (dgvMyRentedComics.IsHandleCreated) clearGrid();
        }

        private void 離開ToolStripMenuItem_Click(object sender, EventArgs e) { this._logger?.Log("「離開」選單項目已點擊。"); Application.Exit(); }

        private void 漫畫管理ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this._logger?.Log("正在開啟漫畫管理表單。");
            ComicManagementForm comicMgmtForm = new ComicManagementForm(this._logger!, this._comicService, this._currentUser);
            comicMgmtForm.ShowDialog(this);
        }

        private void 會員管理ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this._logger?.Log("正在開啟會員管理表單。");
            if (Program.AppAuthService is not null && this._comicService is not null && _memberService is not null)
            {
                MemberManagementForm memberMgmtForm = new MemberManagementForm(this._logger!, this._memberService, Program.AppAuthService, this._comicService, this._currentUser);
                memberMgmtForm.ShowDialog(this);
            }
            else { this._logger?.LogError("AuthenticationService/ComicService/MemberService 為空。無法開啟會員管理表單。"); MessageBox.Show("無法開啟會員管理功能，因為驗證服務未正確初始化。", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void rentalManagementToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this._logger?.Log("正在開啟租借表單。");
            try
            {
                RentalForm rentalForm = new RentalForm(this._comicService, this._memberService, this._logger!, this._reloadService);
                rentalForm.ShowDialog(this);
            }
            catch (Exception ex) { this._logger?.LogError("開啟租借表單失敗。", ex); MessageBox.Show($"開啟租借表單時發生錯誤: {ex.Message}", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void 使用者註冊ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this._logger?.Log("「使用者註冊」選單項目已點擊。");
            if (_currentUser.Role == UserRole.Admin)
            {
                if (this._logger is not null && Program.AppAuthService is not null && this._memberService is not null)
                {
                    var regForm = new RegistrationForm(this._logger, Program.AppAuthService, this._memberService);
                    regForm.ShowDialog(this);
                }
                else { MessageBox.Show("Logger, AuthenticationService, 或 MemberService 未初始化，無法開啟使用者註冊。", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); this._logger?.LogError("由於 logger、AppAuthService 或 _memberService 為空，無法開啟註冊表單。"); }
            }
            else { MessageBox.Show(Constants.UI.MessageBoxTitles.InsufficientPermissions, Constants.UI.MessageBoxTitles.Information, MessageBoxButtons.OK, MessageBoxIcon.Warning); this._logger?.Log($"非管理員使用者 '{_currentUser?.Username}' 嘗試開啟註冊表單。"); }
        }

        private void logoutToolStripMenuItem_Click(object sender, EventArgs e) { this._logger?.Log($"使用者 '{_currentUser?.Username}' 正在登出。"); Application.Restart(); }

        private async void btnRentComic_Click(object? sender, EventArgs e)
        {
            if (_currentUser is null || _comicService is null || _memberService is null || _logger is null)
            { MessageBox.Show("系統元件未正確初始化。無法繼續租借。", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); _logger?.LogError("租借漫畫按鈕點擊事件：關鍵服務或 _currentUser 為空。"); return; }
            if (dgvAvailableComics is null || dgvAvailableComics.SelectedRows.Count == 0)
            { _logger?.Log("租借漫畫按鈕點擊事件：使用者未選擇漫畫。"); MessageBox.Show("請先選擇一本漫畫。", Constants.UI.MessageBoxTitles.Information, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (_currentUser.Role == UserRole.Admin) { _logger?.LogWarning("管理員使用者嘗試點擊租借按鈕。此操作僅供會員使用。"); return; }

            Comic? selectedComic = dgvAvailableComics.SelectedRows[0].DataBoundItem as Comic;
            if (selectedComic is null) { MessageBox.Show("選擇的項目無效或不是有效的漫畫資料。", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); _logger?.LogError("租借漫畫按鈕點擊事件：選取的項目為空或不是有效的漫畫物件。"); return; }
            if (selectedComic?.IsRented == true) { _logger?.Log($"租借漫畫按鈕點擊事件：使用者 '{_currentUser?.Username}' 嘗試租借漫畫 '{selectedComic?.Title}' (ID: {selectedComic?.Id})，但該漫畫已被租借。"); MessageBox.Show($"漫畫 '{selectedComic?.Title}' 已經被借出。", Constants.UI.MessageBoxTitles.Information, MessageBoxButtons.OK, MessageBoxIcon.Information); LoadAvailableComics(); return; }

            DateTime today = DateTime.Today;
            using (RentalPeriodForm rentalDialog = new RentalPeriodForm(today.AddDays(3), today.AddMonths(1)))
            {
                if (rentalDialog.ShowDialog(this) == DialogResult.OK)
                {
                    DateTime selectedReturnDate = rentalDialog.SelectedReturnDate;
                    Member? member = _memberService.GetMemberByUsername(_currentUser.Username);
                    if (member is null) { _logger?.LogWarning($"租借漫畫按鈕點擊事件：找不到使用者名稱為 '{_currentUser?.Username}' 的會員。"); MessageBox.Show($"找不到使用者 '{_currentUser?.Username}' 對應的會員資料。", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

                    selectedComic.IsRented = true; selectedComic.RentedToMemberId = member.Id; selectedComic.RentalDate = DateTime.Now; selectedComic.ReturnDate = selectedReturnDate;
                    try
                    {
                        await _comicService.UpdateComicAsync(selectedComic);
                        _logger?.Log($"漫畫 '{selectedComic?.Title}' (ID: {selectedComic?.Id}) 已租借給會員 ID {member.Id} (使用者名稱: {_currentUser?.Username}) 至 {selectedReturnDate:yyyy-MM-dd}。");
                        MessageBox.Show($"漫畫 '{selectedComic?.Title}' 已成功租借至 {selectedReturnDate:yyyy-MM-dd}。", Constants.UI.MessageBoxTitles.Success, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadAvailableComics(); LoadMyRentedComics();
                        if (dgvAvailableComics is not null) dgvAvailableComics_SelectionChanged(null, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"租借漫畫 '{selectedComic?.Title}' (ID: {selectedComic?.Id}) 給會員 ID {member?.Id} (使用者名稱: {_currentUser?.Username}) 失敗。錯誤: {ex.Message}", ex);
                        MessageBox.Show($"更新漫畫狀態時發生錯誤: {ex.Message}", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        selectedComic.IsRented = false; selectedComic.RentedToMemberId = 0; selectedComic.RentalDate = null; selectedComic.ReturnDate = null;
                    }
                }
                else { _logger?.Log($"使用者 '{_currentUser?.Username}' 已取消漫畫 '{selectedComic?.Title}' 的租借流程。"); }
            }
        }

        private void 檢視日誌ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this._logger?.Log($"使用者 '{_currentUser?.Username}' 點擊了「檢視日誌」選單項目。");
            if (_currentUser.Role == UserRole.Admin)
            {
                this._logger?.Log($"管理員使用者 '{_currentUser?.Username}' 正在檢視日誌。");
                try
                {
                    string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ComicRentalApp", "Logs", Constants.FileNames.LogFile);
                    if (System.IO.File.Exists(logFilePath)) { Process.Start(new ProcessStartInfo(logFilePath) { UseShellExecute = true }); }
                    else { MessageBox.Show("日誌檔案尚未建立或找不到。", Constants.UI.MessageBoxTitles.Information, MessageBoxButtons.OK, MessageBoxIcon.Information); }
                }
                catch (Exception ex) { this._logger?.LogError("開啟日誌檔案失敗。", ex); MessageBox.Show($"無法開啟日誌檔案: {ex.Message}", Constants.UI.MessageBoxTitles.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            else { this._logger?.Log($"使用者 '{_currentUser?.Username}' (角色: {_currentUser?.Role}) 嘗試檢視日誌。權限不足。"); MessageBox.Show(Constants.UI.MessageBoxTitles.InsufficientPermissions, Constants.UI.MessageBoxTitles.Information, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            this._logger?.Log("主表單正在關閉。正在取消訂閱事件。");
            if (this._comicService != null) this._comicService.ComicsChanged -= ComicService_ComicsChanged;
            if (dgvAvailableComics != null) dgvAvailableComics.SelectionChanged -= dgvAvailableComics_SelectionChanged;
            base.OnFormClosing(e);
        }

        private void ApplyAdminComicsView()
        {
            if (_allAdminComicStatuses is null || this.dgvAvailableComics is null) return;
            if (!this.dgvAvailableComics.IsHandleCreated || this.dgvAvailableComics.IsDisposed) return;

            IEnumerable<AdminComicStatusViewModel> viewToShow = _allAdminComicStatuses;
            if (this.cmbAdminComicFilterStatus is not null && this.cmbAdminComicFilterStatus.SelectedItem is not null)
            {
                string? selectedStatus = this.cmbAdminComicFilterStatus.SelectedItem.ToString();
                if (selectedStatus == Constants.UI.FilterOptions.Rented) { viewToShow = viewToShow.Where(vm => vm.Status == Constants.ComicStatuses.Rented); }
                else if (selectedStatus == Constants.UI.FilterOptions.Available) { viewToShow = viewToShow.Where(vm => vm.Status == Constants.ComicStatuses.Available); }
            }
            if (!string.IsNullOrEmpty(_currentSortColumnName))
            {
                var prop = typeof(AdminComicStatusViewModel).GetProperty(_currentSortColumnName);
                if (prop is not null)
                {
                    if (_currentSortDirection == ListSortDirection.Ascending) viewToShow = viewToShow.OrderBy(vm => prop.GetValue(vm, null));
                    else viewToShow = viewToShow.OrderByDescending(vm => prop.GetValue(vm, null));
                }
            }
            var finalViewList = viewToShow.ToList();
            Action updateGridAction = () => {
                dgvAvailableComics.DataSource = null; dgvAvailableComics.DataSource = finalViewList;
                foreach (DataGridViewColumn column in dgvAvailableComics.Columns) column.HeaderCell.SortGlyphDirection = SortOrder.None;
                if (!string.IsNullOrEmpty(_currentSortColumnName) && dgvAvailableComics.Columns.Contains(_currentSortColumnName))
                { dgvAvailableComics.Columns[_currentSortColumnName]!.HeaderCell.SortGlyphDirection = _currentSortDirection == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending; }
            };
            if (this.dgvAvailableComics.InvokeRequired) this.dgvAvailableComics.Invoke(updateGridAction); else updateGridAction();
        }

        private void dgvAvailableComics_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (_currentUser is null || _currentUser.Role != UserRole.Admin || this.dgvAvailableComics is null || e.ColumnIndex < 0 || e.ColumnIndex >= this.dgvAvailableComics.Columns.Count) return;
            string newSortColumnName = this.dgvAvailableComics.Columns[e.ColumnIndex].DataPropertyName;
            if (string.IsNullOrEmpty(newSortColumnName)) return;
            if (_currentSortColumnName == newSortColumnName) _currentSortDirection = (_currentSortDirection == ListSortDirection.Ascending) ? ListSortDirection.Descending : ListSortDirection.Ascending;
            else { _currentSortColumnName = newSortColumnName; _currentSortDirection = ListSortDirection.Ascending; }
            ApplyAdminComicsView();
        }

        private void cmbAdminComicFilterStatus_SelectedIndexChanged(object? sender, EventArgs e) { if (_currentUser is null || _currentUser.Role != UserRole.Admin) return; ApplyAdminComicsView(); }

        private void HideAllMainContentViews()
        {
            if (_adminDashboardControl is not null) _adminDashboardControl.Visible = false;
            if (memberViewTabControl is not null) memberViewTabControl.Visible = false;
            if (lblAvailableComics is not null) lblAvailableComics.Visible = false;
            if (cmbAdminComicFilterStatus is not null) cmbAdminComicFilterStatus.Visible = false;
            // dgvAvailableComics visibility is complex due to shared use; managed by context
        }

        private void ShowDashboardView()
        {
            HideAllMainContentViews();
            if (_adminDashboardControl is not null)
            {
                _adminDashboardControl.Visible = true; _adminDashboardControl.BringToFront();
                _adminDashboardControl.LoadDashboardData();
                this.Text = Constants.UI.FormTitles.AdminBaseTitle + Constants.UI.FormTitles.Dashboard;
            }
            _logger?.Log("已選取儀表板視圖。");
        }

        private void ShowAdminComicManagementView()
        {
            HideAllMainContentViews();
            if (mainContentPanel is null) return;
            if (lblAvailableComics is not null && lblAvailableComics.Parent != mainContentPanel) mainContentPanel.Controls.Add(lblAvailableComics);
            if (cmbAdminComicFilterStatus is not null && cmbAdminComicFilterStatus.Parent != mainContentPanel) mainContentPanel.Controls.Add(cmbAdminComicFilterStatus);
            if (dgvAvailableComics is not null && dgvAvailableComics.Parent != mainContentPanel) mainContentPanel.Controls.Add(dgvAvailableComics);

            var panelWidth = mainContentPanel.ClientSize.Width; var panelHeight = mainContentPanel.ClientSize.Height; const int margin = 8;
            if (lblAvailableComics is not null) { lblAvailableComics.SetBounds(margin, margin, panelWidth - 2 * margin, 28); lblAvailableComics.Visible = true; }
            if (cmbAdminComicFilterStatus is not null) { int comboWidth = 120, comboHeight = 23, comboX = panelWidth - comboWidth - margin, comboY = (lblAvailableComics?.Bottom ?? margin) + 8; cmbAdminComicFilterStatus.SetBounds(comboX, comboY, comboWidth, comboHeight); cmbAdminComicFilterStatus.Visible = true; }
            if (dgvAvailableComics is not null) { int dgvY = (cmbAdminComicFilterStatus?.Bottom ?? lblAvailableComics?.Bottom ?? margin) + 8, dgvHeight = panelHeight - dgvY - margin; dgvAvailableComics.SetBounds(margin, dgvY, panelWidth - 2 * margin, dgvHeight); dgvAvailableComics.Visible = true; dgvAvailableComics.BringToFront(); }
            this.Text = Constants.UI.FormTitles.AdminBaseTitle + Constants.UI.FormTitles.ComicManagement;
            _logger?.Log("已選取漫畫管理視圖。");
        }

        private void SelectNavButton(Button selectedButton)
        {
            if (_currentSelectedNavButton is not null) { _currentSelectedNavButton.BackColor = ModernBaseForm.SecondaryColor; _currentSelectedNavButton.ForeColor = ModernBaseForm.TextColor; _currentSelectedNavButton.Font = ModernBaseForm.ButtonFont ?? new System.Drawing.Font("Segoe UI Semibold", 9.75F); }
            selectedButton.BackColor = ModernBaseForm.PrimaryColor; selectedButton.ForeColor = System.Drawing.Color.White; var baseFont = ModernBaseForm.ButtonFont ?? new System.Drawing.Font("Segoe UI Semibold", 9.75F); selectedButton.Font = new System.Drawing.Font(baseFont, System.Drawing.FontStyle.Bold); _currentSelectedNavButton = selectedButton;

            if (dgvAvailableComics is not null && _currentUser?.Role == UserRole.Admin && selectedButton != btnNavComicMgmt) { /* dgvAvailableComics.Visible = false; */ } // Special handling for admin dgv visibility

            if (selectedButton == btnNavDashboard) ShowDashboardView();
            else if (selectedButton == btnNavComicMgmt) ShowAdminComicManagementView();
            else if (selectedButton == btnNavMemberMgmt) { HideAllMainContentViews(); this.會員管理ToolStripMenuItem_Click(this, EventArgs.Empty); _logger?.Log("會員管理導覽按鈕已點擊。"); }
            else if (selectedButton == btnNavRentalMgmt) { HideAllMainContentViews(); this.rentalManagementToolStripMenuItem_Click(this, EventArgs.Empty); _logger?.Log("租借管理導覽按鈕已點擊。"); }
            else if (selectedButton == btnNavUserReg) { HideAllMainContentViews(); this.使用者註冊ToolStripMenuItem_Click(this, EventArgs.Empty); _logger?.Log("使用者註冊導覽按鈕已點擊。"); }
            else if (selectedButton == btnNavLogs) { HideAllMainContentViews(); this.檢視日誌ToolStripMenuItem_Click(this, EventArgs.Empty); _logger?.Log("檢視日誌導覽按鈕已點擊。"); }
        }

        private void btnNavDashboard_Click(object? sender, EventArgs e) { if (sender is Button cb) SelectNavButton(cb); }
        private void btnNavComicMgmt_Click(object? sender, EventArgs e) { if (sender is Button cb) SelectNavButton(cb); }
        private void btnNavMemberMgmt_Click(object? sender, EventArgs e) { if (sender is Button cb) SelectNavButton(cb); }
        private void btnNavRentalMgmt_Click(object? sender, EventArgs e) { if (sender is Button cb) SelectNavButton(cb); }
        private void btnNavUserReg_Click(object? sender, EventArgs e) { if (sender is Button cb) SelectNavButton(cb); }
        private void btnNavLogs_Click(object? sender, EventArgs e) { if (sender is Button cb) SelectNavButton(cb); }

        private void dgvAvailableComics_AdminView_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_currentUser is null || _currentUser.Role != UserRole.Admin || dgvAvailableComics is null || e.RowIndex < 0 || e.RowIndex >= dgvAvailableComics.Rows.Count) return;
            DataGridViewRow row = dgvAvailableComics.Rows[e.RowIndex];
            if (row.DataBoundItem is not AdminComicStatusViewModel comicStatus) return;

            if (dgvAvailableComics.Columns[e.ColumnIndex].DataPropertyName == Constants.DataPropertyNames.Status)
            {
                if (e.Value?.ToString() == Constants.ComicStatuses.Rented) e.CellStyle.ForeColor = ModernBaseForm.DangerColor;
                else if (e.Value?.ToString() == Constants.ComicStatuses.Available) e.CellStyle.ForeColor = ModernBaseForm.SuccessColor;
            }
            if (dgvAvailableComics.Columns[e.ColumnIndex].DataPropertyName == Constants.DataPropertyNames.ReturnDate || dgvAvailableComics.Columns[e.ColumnIndex].DataPropertyName == Constants.DataPropertyNames.Status)
            {
                if (comicStatus?.Status == Constants.ComicStatuses.Rented && comicStatus?.ReturnDate.HasValue == true)
                {
                    DateTime returnDate = comicStatus.ReturnDate.Value;
                    if (returnDate.Date < DateTime.Today) { row.DefaultCellStyle.BackColor = ModernBaseForm.DangerColor; row.DefaultCellStyle.ForeColor = Color.White; }
                    else if (returnDate.Date <= DateTime.Today.AddDays(3)) { row.DefaultCellStyle.BackColor = ModernBaseForm.AccentColor; row.DefaultCellStyle.ForeColor = ModernBaseForm.TextColor; }
                }
            }
        }

        private void dgvMyRentedComics_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvMyRentedComics is null || e.RowIndex < 0 || e.RowIndex >= dgvMyRentedComics.Rows.Count) return;
            DataGridViewRow row = dgvMyRentedComics.Rows[e.RowIndex];
            if (row.DataBoundItem is null) return;
            DateTime? returnDate = null;
            try { var val = row.DataBoundItem.GetType().GetProperty(Constants.DataPropertyNames.ExpectedReturnDate)?.GetValue(row.DataBoundItem, null); if (val is not null && val != DBNull.Value) returnDate = Convert.ToDateTime(val); }
            catch (Exception ex) { _logger?.LogError($"在 CellFormatting 中透過反映取得 ExpectedReturnDate 時發生錯誤: {ex.Message}"); return; }
            if (returnDate is null) return;

            if (dgvMyRentedComics.Columns[e.ColumnIndex].Name == "statusColumn")
            {
                TimeSpan remainingTime = returnDate.Value.Date - DateTime.Today;
                if (remainingTime.TotalDays < 0) e.Value = $"{Constants.ComicStatuses.OverduePrefix}{-remainingTime.TotalDays}{Constants.ComicStatuses.DaysSuffix}";
                else if (remainingTime.TotalDays == 0) e.Value = Constants.ComicStatuses.DueToday;
                else e.Value = $"{Constants.ComicStatuses.RemainingPrefix}{remainingTime.TotalDays}{Constants.ComicStatuses.DaysSuffix}";
                e.FormattingApplied = true;
            }
            if (returnDate.Value.Date < DateTime.Today) { e.CellStyle.BackColor = ModernBaseForm.DangerColor; e.CellStyle.ForeColor = Color.White; }
            else if (returnDate.Value.Date <= DateTime.Today.AddDays(3)) { e.CellStyle.BackColor = ModernBaseForm.AccentColor; e.CellStyle.ForeColor = ModernBaseForm.TextColor; }
        }

        private void memberViewTabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (memberViewTabControl is null) return;
            if (memberViewTabControl.SelectedTab == myRentalsTabPage) { _logger?.Log("已選取「我的租借」標籤頁。正在重新載入已租借漫畫。"); LoadMyRentedComics(); }
            else if (memberViewTabControl.SelectedTab == availableComicsTabPage) { _logger?.Log("已選取「可租借漫畫」標籤頁。正在重新套用篩選器。"); ApplyAvailableComicsFilter(); }
        }

        private bool IsMemberViewActive() { return _currentUser?.Role == UserRole.Member && memberViewTabControl?.Visible == true && memberViewTabControl.SelectedTab == availableComicsTabPage; }

        private void ApplyAvailableComicsFilter()
        {
            if (_comicService is null || dgvAvailableComics is null || _currentUser is null) { _logger?.LogWarning("套用可租借漫畫篩選器：關鍵元件為空，略過篩選。"); return; }
            try
            {
                _logger?.Log("正在套用可租借漫畫篩選器…");
                string placeholder = Constants.UI.Placeholders.SearchComicsPlaceholder;
                string searchText = (txtSearchAvailableComics is not null && txtSearchAvailableComics.Text != placeholder && !string.IsNullOrWhiteSpace(txtSearchAvailableComics.Text)) ? txtSearchAvailableComics.Text.Trim().ToLowerInvariant() : "";
                var comics = _comicService.GetAllComics().Where(c => !c.IsRented);
                if (!string.IsNullOrWhiteSpace(searchText)) { comics = comics.Where(c => (c.Title?.ToLowerInvariant().Contains(searchText) ?? false) || (c.Author?.ToLowerInvariant().Contains(searchText) ?? false)); }
                string genre = (cmbGenreFilter?.SelectedIndex > 0 && cmbGenreFilter.SelectedItem is string gs && gs != Constants.UI.FilterOptions.AllGenres) ? gs : Constants.UI.FilterOptions.AllGenres;
                if (genre != Constants.UI.FilterOptions.AllGenres) { comics = comics.Where(c => !string.IsNullOrWhiteSpace(c.Genre) && c.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase)); }
                var filteredList = comics.ToList();
                Action update = () => { dgvAvailableComics.DataSource = null; dgvAvailableComics.DataSource = filteredList; };
                if (dgvAvailableComics.IsHandleCreated && !dgvAvailableComics.IsDisposed) { if (dgvAvailableComics.InvokeRequired) dgvAvailableComics.Invoke(update); else update(); }
                _logger?.Log($"篩選完成：搜尋「{searchText}」、類型「{genre}」→ 共 {filteredList.Count} 本。");
            }
            catch (Exception ex)
            {
                _logger?.LogError("套用可租借漫畫篩選器時發生錯誤。", ex);
                if (dgvAvailableComics != null && dgvAvailableComics.IsHandleCreated && !dgvAvailableComics.IsDisposed)
                { Action clear = () => dgvAvailableComics.DataSource = null; if (dgvAvailableComics.InvokeRequired) dgvAvailableComics.Invoke(clear); else clear(); }
            }
        }
    }
}
