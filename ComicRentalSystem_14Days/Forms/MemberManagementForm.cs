using ComicRentalSystem_14Days.Models;
using ComicRentalSystem_14Days.Services;
using ComicRentalSystem_14Days.Helpers;
using ComicRentalSystem_14Days.Interfaces;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ComicRentalSystem_14Days.Forms
{
    public partial class MemberManagementForm : ComicRentalSystem_14Days.BaseForm
    {
        private MemberService? _memberService;
        private AuthenticationService? _authenticationService;
        private readonly ComicService? _comicService;
        private readonly User? _currentUser;

        public MemberManagementForm()
        {
            InitializeComponent();
        }

        public MemberManagementForm(ILogger logger, MemberService memberService, AuthenticationService authenticationService, ComicService comicService, User? currentUser) : base(logger)
        {
            InitializeComponent();
            _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _comicService = comicService ?? throw new ArgumentNullException(nameof(comicService));
            _currentUser = currentUser;
            LogActivity("會員管理表單正在使用 MemberService、AuthenticationService 和 ComicService 初始化。");
        }

        private void MemberManagementForm_Load(object sender, EventArgs e)
        {
            if (this.DesignMode || Logger == null || _memberService == null || _authenticationService == null || _comicService == null) // 已為 _authenticationService 和 _comicService 新增 null 檢查
            {
                return;
            }

            LogActivity("MemberManagementForm is loading runtime components.");

            _memberService.MembersChanged += MemberService_MembersChanged; 

            SetupDataGridView(); 
            LoadMembersData(); 

            LogActivity("會員管理表單已成功初始化。");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            LogActivity($"MemberManagementForm closing. User: {_currentUser?.Username ?? "N/A"}");
            if (_memberService != null)
            {
                _memberService.MembersChanged -= MemberService_MembersChanged;
                LogActivity("已取消訂閱 MemberService.MembersChanged 事件。");
            }
            base.OnFormClosing(e);
        }

        private void MemberService_MembersChanged(object? sender, EventArgs e)
        {
            if (_memberService == null) return;
            LogActivity("已收到 MembersChanged 事件。正在重新整理 DataGridView。");
            LoadMembersData();
        }

        private void SetupDataGridView()
        {
            LogActivity("正在設定會員的 DataGridView 資料行。");
            dgvMembers.AutoGenerateColumns = false;
            dgvMembers.Columns.Clear();

            dgvMembers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 50 });
            dgvMembers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "姓名", Width = 200, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvMembers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PhoneNumber", HeaderText = "電話號碼", Width = 150 });

            dgvMembers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMembers.MultiSelect = false;
            dgvMembers.ReadOnly = true;
            dgvMembers.AllowUserToAddRows = false;
            LogActivity("會員的 DataGridView 設定完成。");
        }

        private void LoadMembersData()
        {
            if (_memberService == null) return;

            string searchTerm = this.txtSearchMembers.Text.Trim();

            LogActivity($"嘗試載入會員資料。搜尋關鍵字: '{searchTerm}'。");

            try
            {
                List<Member> members;
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    members = _memberService.GetAllMembers();
                }
                else
                {
                    members = _memberService.SearchMembers(searchTerm);
                }

                Action updateGrid = () => {
                    dgvMembers.DataSource = null;
                    dgvMembers.DataSource = members;
                };

                if (dgvMembers.IsHandleCreated && !dgvMembers.IsDisposed)
                {
                    if (dgvMembers.InvokeRequired)
                    {
                        dgvMembers.Invoke(updateGrid);
                    }
                    else
                    {
                        updateGrid();
                    }
                }
                LogActivity($"已成功使用搜尋關鍵字 '{searchTerm}' 將 {members.Count} 位會員載入 DataGridView。");
            }
            catch (Exception ex)
            {
                LogErrorActivity($"使用搜尋關鍵字 '{searchTerm}' 載入會員資料時發生錯誤。", ex);
                Action showError = () => MessageBox.Show($"載入會員資料時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    if (this.InvokeRequired) { this.Invoke(showError); } else { showError(); }
                }
            }
        }

        private void btnSearchMembers_Click(object? sender, EventArgs e)
        {
            LogActivity("搜尋會員按鈕已點擊。");
            LoadMembersData();
        }

        private void btnClearSearchMembers_Click(object? sender, EventArgs e)
        {
            LogActivity("清除搜尋會員按鈕已點擊。");
            this.txtSearchMembers.Text = string.Empty;
            LoadMembersData();
        }

        private async void btnRefreshMembers_Click(object sender, EventArgs e) // 設為 async void
        {
            if (_memberService == null) return;
            LogActivity("Refresh Members button clicked. Will reload members from file asynchronously.");
            try
            {
                await _memberService.ReloadAsync(); 
            }
            catch (Exception ex)
            {
                LogErrorActivity("Error refreshing members data from file.", ex);
                MessageBox.Show($"重新載入會員資料時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddMember_Click(object sender, EventArgs e)
        {
            if (_memberService == null || Logger == null || _authenticationService == null)
            {
                LogErrorActivity("新增會員所需的基本服務不可用。", new InvalidOperationException("服務未初始化。"));
                MessageBox.Show("無法開啟註冊表單，必要的服務未初始化。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            LogActivity("新增會員按鈕已點擊。正在為新會員開啟 RegistrationForm。");
            using (RegistrationForm regForm = new RegistrationForm(Logger, _authenticationService, _memberService, _currentUser))
            {
                if (regForm.ShowDialog(this) == DialogResult.OK)
                {
                    LogActivity("RegistrationForm (新增會員) 已關閉並回傳 OK。資料重新整理將由 MembersChanged 事件處理。");
                    LoadMembersData();
                }
                else
                {
                    LogActivity("RegistrationForm (新增會員) 已關閉並回傳 Cancel 或其他。");
                }
            }
        }

        private void btnEditMember_Click(object sender, EventArgs e)
        {
            if (dgvMembers.SelectedRows.Count > 0 && _memberService != null && Logger != null)
            {
                Member? selectedMember = dgvMembers.SelectedRows[0].DataBoundItem as Member;

                if (selectedMember != null)
                {
                    LogActivity($"正在為編輯會員 ID: {selectedMember.Id}，姓名: '{selectedMember.Name}' 開啟 MemberEditForm。");
                    using (MemberEditForm editForm = new MemberEditForm(selectedMember, _memberService, Logger))
                    {
                        if (editForm.ShowDialog(this) == DialogResult.OK)
                        {
                            LogActivity($"會員 ID: {selectedMember.Id} 的 MemberEditForm (編輯模式) 已關閉並回傳 OK。資料重新整理將由 MembersChanged 事件處理。");
                        }
                        else
                        {
                            LogActivity($"會員 ID: {selectedMember.Id} 的 MemberEditForm (編輯模式) 已關閉並回傳 Cancel 或其他。");
                        }
                    }
                }
                else
                {
                    LogErrorActivity("無法從 DataGridView 擷取選定的會員資料進行編輯。");
                    MessageBox.Show("無法取得選定的會員資料。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                LogActivity("編輯會員按鈕已點擊，但未選取任何會員或服務未就緒。");
                MessageBox.Show("請先選擇一位要編輯的會員。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void btnDeleteMember_Click(object sender, EventArgs e) // Changed to async void
        {
            if (dgvMembers.SelectedRows.Count > 0 && _memberService != null)
            {
                Member? selectedMember = dgvMembers.SelectedRows[0].DataBoundItem as Member;
                if (selectedMember != null)
                {
                    LogActivity($"Attempting to delete member ID: {selectedMember.Id}, Name: '{selectedMember.Name}'.");

                    // Check for active rentals
                    if (_comicService != null)
                    {
                        bool hasActiveRentals = _comicService.GetAllComics().Any(c => c.IsRented && c.RentedToMemberId == selectedMember.Id);
                        if (hasActiveRentals)
                        {
                            MessageBox.Show($"會員 '{selectedMember.Name}' (ID: {selectedMember.Id}) 尚有未歸還的漫畫，無法刪除。", "刪除錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            LogActivity($"嘗試刪除會員 ID: {selectedMember.Id}，姓名: '{selectedMember.Name}' 失敗：會員尚有租借中的漫畫。");
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("無法檢查會員租借狀態，漫畫服務未初始化。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        LogErrorActivity("Could not check member rental status: _comicService is null.");
                        return;
                    }

                    LogActivity($"嘗試刪除會員 ID: {selectedMember.Id}，姓名: '{selectedMember.Name}'。正在顯示確認對話方塊。");
                    var confirmResult = MessageBox.Show($"您確定要刪除會員 '{selectedMember.Name}' (ID: {selectedMember.Id}) 嗎？\n此操作無法復原。", "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirmResult == DialogResult.Yes)
                    {
                        LogActivity($"使用者已確認刪除會員 ID: {selectedMember.Id}。");
                        try
                        {
                            await _memberService.DeleteMemberAsync(selectedMember.Id); // Changed to await async version
                            string usernameToDelete = selectedMember.Username; 
                            
                            // Note: _authenticationService.DeleteUser is still synchronous.
                            // If it were async, this whole block would need more careful async/await handling.
                            if (_authenticationService != null)
                            {
                                bool userDeleted = _authenticationService.DeleteUser(usernameToDelete);
                                if (userDeleted)
                                {
                                    LogActivity($"會員 ID: {selectedMember.Id} 及其關聯的使用者帳戶 '{selectedMember.Username}' 已由服務成功標記為待刪除。UI 將透過事件重新整理。");
                                    MessageBox.Show($"會員 '{selectedMember.Name}' (使用者名稱: '{usernameToDelete}') 及其使用者帳戶已刪除。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    Logger?.LogWarning($"User account for Username: '{usernameToDelete}' (Member Name: '{selectedMember.Name}') not found by AuthenticationService, though member record was deleted. Possible data inconsistency if a user account was expected.");
                                    LogActivity($"會員 ID: {selectedMember.Id} (無使用者帳戶) 已由服務成功標記為待刪除。UI 將透過事件重新整理。"); // Log for member deletion without user
                                    MessageBox.Show($"會員 '{selectedMember.Name}' 已從會員列表中刪除，但對應的使用者帳戶 '{usernameToDelete}' 未找到或無法刪除。", "部分成功", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }
                            else
                            {
                                LogErrorActivity("AuthenticationService is null. Cannot delete user account.", new InvalidOperationException("_authenticationService is null")); // This is one of the targeted exception messages
                                MessageBox.Show($"會員 '{selectedMember.Name}' 已從會員列表中刪除，但由於內部錯誤無法刪除其使用者帳戶。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        catch (InvalidOperationException opEx)
                        {
                            LogErrorActivity($"刪除會員 ID: {selectedMember.Id} 時發生操作錯誤。", opEx);
                            MessageBox.Show(opEx.Message, "操作錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (Exception ex)
                        {
                            LogErrorActivity($"刪除會員 ID: {selectedMember.Id} 時發生一般錯誤。", ex);
                            MessageBox.Show($"刪除會員時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        LogActivity($"使用者已取消刪除會員 ID: {selectedMember.Id}。");
                    }
                }
            }
            else
            {
                LogActivity("刪除會員按鈕已點擊，但未選取任何會員或服務未就緒。");
                MessageBox.Show("請先選擇一位要刪除的會員。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void dgvMembers_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                LogActivity($"DataGridView 儲存格在資料列 {e.RowIndex} 被雙擊。觸發會員編輯動作。");
                btnEditMember_Click(sender, e);
            }
        }

        private void btnChangeUserRole_Click(object? sender, EventArgs e)
        {
            if (dgvMembers.SelectedRows.Count == 0)
            {
                MessageBox.Show("請選擇一位會員。", "未選擇會員", MessageBoxButtons.OK, MessageBoxIcon.Information); 
                return;
            }
            if (_authenticationService == null || Logger == null) 
            {
                 MessageBox.Show("必要的服務不可用。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                 LogErrorActivity("變更使用者角色所需服務不可用。", new InvalidOperationException("服務未初始化。"));
                 return;
            }

            Member? selectedMember = dgvMembers.SelectedRows[0].DataBoundItem as Member;
            if (selectedMember == null || string.IsNullOrEmpty(selectedMember.Username))
            {
                MessageBox.Show("選定的會員沒有關聯的使用者名稱或資料無效。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogErrorActivity("Selected member for role change has no username or is invalid.");
                return;
            }

            User? userToEdit = _authenticationService.GetUserByUsername(selectedMember.Username);
            if (userToEdit == null)
            {
                MessageBox.Show($"找不到使用者帳戶 '{selectedMember.Username}'。", "找不到使用者", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                LogErrorActivity($"User account for member '{selectedMember.Name}' (username: {selectedMember.Username}) not found for role change.");
                return;
            }

            LogActivity($"Opening ChangeUserRoleForm for member '{selectedMember.Name}', user '{userToEdit.Username}'.");
            using (ChangeUserRoleForm changeRoleForm = new ChangeUserRoleForm(userToEdit, _authenticationService, Logger))
            {
                changeRoleForm.ShowDialog(this);
            }
        }
    }
}