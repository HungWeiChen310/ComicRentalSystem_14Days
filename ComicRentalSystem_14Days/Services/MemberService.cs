using ComicRentalSystem_14Days.Helpers;
using ComicRentalSystem_14Days.Interfaces;
using ComicRentalSystem_14Days.Models;
using System;
using System.Collections.Generic;
using System.IO; 
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Forms;

namespace ComicRentalSystem_14Days.Services
{
    public class MemberService
    {
        private readonly IFileHelper _fileHelper; 
        private readonly string _memberFileName = Constants.FileNames.Members; // Replaced
        private List<Member> _members = new List<Member> { };
        private readonly ILogger _logger;
        private readonly ComicService _comicService;
        private readonly object _membersLock = new object(); // Added lock object

        public delegate void MemberDataChangedEventHandler(object? sender, EventArgs e);
        public event MemberDataChangedEventHandler? MembersChanged;

        public static async Task<MemberService> CreateAsync(IFileHelper fileHelper, ILogger logger, ComicService comicService)
        {
            var service = new MemberService(fileHelper, logger, comicService);
            await service.ReloadAsync(); // Ensure members are loaded on creation
            return service;
        }

        private MemberService(IFileHelper fileHelper, ILogger? logger, ComicService comicService)
        {
            _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "MemberService 的記錄器不可為空。");
            _comicService = comicService ?? throw new ArgumentNullException(nameof(comicService)); 

            _logger.Log("MemberService 初始化中。");

            LoadMembersFromFile(); // Synchronous load during construction
            _logger.Log($"MemberService 初始化完成。已載入 {_members.Count} 位會員。");
        }

        public async Task ReloadAsync() 
        {
            _logger.Log("MemberService 已呼叫 ReloadAsync。");
            List<Member> loadedMembers = await LoadMembersAsync(); // Changed to List<Member>
            _members = loadedMembers; // Assign loaded members
            OnMembersChanged();
            _logger.Log($"MemberService 已非同步重新載入。已載入 {_members.Count} 位會員。");
        }

        private void LoadMembersFromFile() 
        {
            _logger.Log($"正在嘗試從檔案載入會員 (同步): '{_memberFileName}'。");
            try
            {
                string csvData = _fileHelper.ReadFile(_memberFileName);
                _members = ParseMembersFromCsv(csvData);
                _logger.Log($"成功從 '{_memberFileName}' (同步) 載入 {_members.Count} 位會員。");
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                _logger.LogError($"嚴重錯誤: 會員資料檔案 '{_memberFileName}' (同步) 已損壞或無法讀取。詳細資訊: {ex.Message}", ex);
                throw new ApplicationException($"無法從 '{_memberFileName}' (同步) 載入會員資料。應用程式可能無法正常運作。", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"從 '{_memberFileName}' (同步) 載入會員時發生未預期的錯誤。詳細資訊: {ex.Message}", ex);
                throw new ApplicationException("載入會員資料期間 (同步) 發生未預期錯誤。", ex);
            }
        }

        private List<Member> ParseMembersFromCsv(string csvData)
        {
            var membersList = new List<Member>();
            if (string.IsNullOrWhiteSpace(csvData))
            {
                _logger.LogWarning($"CSV 會員資料為空或僅包含空白字元。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.");
                return membersList;
            }

            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // lines.Length will be 0 if csvData is empty but not whitespace (e.g. only contains \r\n)
            // or if Split results in no entries after removing empty ones.
            if (lines.Length == 0)
            {
                _logger.LogWarning($"CSV 會員資料不包含任何有效行 (可能是空的或僅包含標頭)。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.");
                return membersList;
            }

            // Skip header row by starting loop from 1 (i.e. lines[0] is header)
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    membersList.Add(Member.FromCsvString(line));
                }
                catch (FormatException formatEx)
                {
                    // Log with line number (i + 1 because lines are 1-indexed for human/editor view, and we skipped header)
                    _logger.LogError($"解析會員 CSV 行失敗 (行號: {i + 1}) for file '{_memberFileName}' (Full path: '{_fileHelper.GetFullFilePath(_memberFileName)}'): '{line}'. 錯誤: {formatEx.Message}", formatEx);
                }
            }
            return membersList;
        }

        private async Task<List<Member>> LoadMembersAsync()
        {
            _logger.Log($"正在嘗試從檔案非同步載入會員: '{_memberFileName}'。");
            try
            {
                string csvData = await _fileHelper.ReadFileAsync(_memberFileName);
                if (string.IsNullOrWhiteSpace(csvData) && csvData != null) // Check if it's not null but empty
                {
                     _logger.LogWarning($"會員檔案 '{_memberFileName}' (非同步) 為空。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.");
                     return new List<Member>();
                }
                else if(csvData == null) // Check if file doesn't exist or unreadable leading to null
                {
                     _logger.LogWarning($"會員檔案 '{_memberFileName}' (非同步) 讀取失敗或不存在。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.");
                     return new List<Member>();
                }


                var loadedMembers = ParseMembersFromCsv(csvData);
                _logger.Log($"成功從 '{_memberFileName}' (非同步) 載入並解析 {loadedMembers.Count} 位會員。");
                return loadedMembers;
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning($"會員檔案 '{_memberFileName}' (非同步) 找不到。返回空列表。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.");
                return new List<Member>();
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"讀取會員檔案 '{_memberFileName}' (非同步) 時發生IO錯誤: {ioEx.Message}。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.", ioEx);
                return new List<Member>(); 
            }
            catch (Exception ex)
            {
                _logger.LogError($"從 '{_memberFileName}' (非同步) 載入會員時發生未預期的錯誤: {ex.Message}。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'.", ex);
                return new List<Member>(); 
            }
        }

        // Made private, will be replaced by SaveMembersAsync for primary use
        private void SaveMembers()
        {
            _logger.Log($"[同步儲存] 正在嘗試將 {_members.Count} 位會員儲存到檔案: '{_memberFileName}'。");
            // This method assumes _members is already locked if necessary by the caller, or that it's inherently safe.
            // For consistency with new async save, direct manipulation of _members should be within a lock.
            // However, since this is becoming private and unused, extensive changes are not made.
            try
            {
                _fileHelper.WriteFile<Member>(_memberFileName, new List<Member>(_members), member => member.ToCsvString()); // Save a copy
                _logger.Log($"[同步儲存] 已成功將 {_members.Count} 位會員儲存到 '{_memberFileName}'。");
                OnMembersChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[同步儲存] 將 {(_members != null ? _members.Count : 0)} 位會員儲存到 '{_memberFileName}' 時發生錯誤。檔案路徑: '{_fileHelper.GetFullFilePath(_memberFileName)}'. 詳細資訊: {ex.Message}", ex);
                throw;
            }
        }

        public async Task SaveMembersAsync()
        {
            int membersCount = 0;
            string fullPath = string.Empty;
            try
            {
                List<Member> membersToSave;
                lock (_membersLock) // Lock for reading _members list
                {
                    membersToSave = new List<Member>(_members); // Create a copy to save
                }
                membersCount = membersToSave.Count;
                fullPath = _fileHelper.GetFullFilePath(_memberFileName);

                _logger.Log($"[非同步儲存] 正在嘗試將 {membersCount} 位會員儲存到檔案: '{_memberFileName}'. Full path: '{fullPath}'.");

                await _fileHelper.WriteFileAsync<Member>(_memberFileName, membersToSave, member => member.ToCsvString());

                _logger.Log($"[非同步儲存] 已成功將 {membersCount} 位會員儲存到 '{_memberFileName}'. Full path: '{fullPath}'.");
                OnMembersChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[非同步儲存] 將 {membersCount} 位會員儲存到 '{_memberFileName}' (Full path: '{fullPath}') 時發生錯誤。", ex);
                throw;
            }
        }

        protected virtual void OnMembersChanged()
        {
            MembersChanged?.Invoke(this, EventArgs.Empty);
            _logger.Log("已觸發 MembersChanged 事件。");
        }

        public List<Member> GetAllMembers()
        {
            _logger.Log("已呼叫 GetAllMembers。");
            // Return a copy to prevent external modification of the internal list if accessed without lock
            lock(_membersLock)
            {
                return new List<Member>(_members);
            }
        }

        public Member? GetMemberById(int id)
        {
            _logger.Log($"已為ID: {id} 呼叫 GetMemberById。");
            Member? member;
            lock(_membersLock)
            {
                member = _members.FirstOrDefault(m => m.Id == id);
            }

            if (member == null)
            {
                _logger.Log($"找不到ID為: {id} 的會員。");
            }
            else
            {
                _logger.Log($"找到ID為: {id} 的會員: 姓名='{member.Name}'。");
            }
            return member;
        }

        public async Task AddMemberAsync(Member member)
        {
            if (member == null)
            {
                var ex = new ArgumentNullException(nameof(member));
                _logger.LogError("嘗試新增空的會員物件。", ex);
                throw ex;
            }

            _logger.Log($"正在嘗試新增會員: 姓名='{member.Name}', 電話號碼='{member.PhoneNumber}'。");

            lock (_membersLock)
            {
                if (member.Id != 0 && _members.Any(m => m.Id == member.Id))
                {
                    var ex = new InvalidOperationException($"ID為 {member.Id} 的會員已存在。");
                    _logger.LogError($"新增會員失敗: ID {member.Id} (姓名='{member.Name}') 已存在。", ex);
                    throw ex;
                }
                // Need to be careful with LINQ operations on _members if they are not thread-safe.
                // For simple Any/First, it might be okay if writes are also locked.
                if (_members.Any(m => m.PhoneNumber == member.PhoneNumber))
                {
                    _logger.LogWarning($"電話號碼為 '{member.PhoneNumber}' 的會員已存在 (姓名='{_members.First(m => m.PhoneNumber == member.PhoneNumber).Name}')。繼續新增。");
                }

                if (member.Id == 0)
                {
                    member.Id = GetNextIdInternal(); // Assumes GetNextIdInternal is safe within this lock
                    _logger.Log($"已為會員 '{member.Name}' 產生新的ID {member.Id}。");
                }

                _members.Add(member);
                _logger.Log($"會員 '{member.Name}' (ID: {member.Id}) 已新增至記憶體列表。會員總數: {_members.Count}。");
            }
            await SaveMembersAsync();
        }

        public async Task UpdateMemberAsync(Member member)
        {
            if (member == null)
            {
                var ex = new ArgumentNullException(nameof(member));
                _logger.LogError("嘗試使用空的會員物件進行更新。", ex);
                throw ex;
            }

            _logger.Log($"正在嘗試更新ID為: {member.Id} (姓名='{member.Name}') 的會員。");
            bool needsSave = false;
            lock (_membersLock)
            {
                Member? existingMember = _members.FirstOrDefault(m => m.Id == member.Id);
                if (existingMember == null)
                {
                    var ex = new InvalidOperationException($"找不到ID為 {member.Id} 的會員進行更新。");
                    _logger.LogError($"更新會員失敗: 找不到ID {member.Id} (姓名='{member.Name}')。", ex);
                    throw ex;
                }

                if (existingMember.Name != member.Name || existingMember.PhoneNumber != member.PhoneNumber ||
                    existingMember.Username != member.Username /* Add other properties if they exist */)
                {
                    existingMember.Name = member.Name;
                    existingMember.PhoneNumber = member.PhoneNumber;
                    existingMember.Username = member.Username; // Assuming Username can be updated
                    // Update other properties as necessary
                    _logger.Log($"ID {member.Id} (姓名='{existingMember.Name}') 的會員屬性已在記憶體中更新。");
                    needsSave = true;
                }
                else
                {
                     _logger.Log($"ID {member.Id} (姓名='{existingMember.Name}') 的會員屬性未變更。略過儲存。");
                }
            }

            if(needsSave)
            {
                await SaveMembersAsync();
                _logger.Log($"ID為: {member.Id} 的會員更新已請求非同步保存。");
            }
        }

        public async Task DeleteMemberAsync(int id)
        {
            _logger.Log($"正在嘗試刪除ID為: {id} 的會員。");
            bool removed = false;
            string? memberName = null;
            lock (_membersLock)
            {
                Member? memberToRemove = _members.FirstOrDefault(m => m.Id == id);

                if (memberToRemove == null)
                {
                    var ex = new InvalidOperationException($"找不到ID為 {id} 的會員進行刪除。");
                    _logger.LogError($"刪除會員失敗: 找不到ID {id}。", ex);
                    throw ex;
                }

                // Check for rentals - this logic needs _comicService which is fine to call outside lock if it's thread-safe
                // For safety, if _comicService.GetAllComics() isn't thread-safe, it should be called outside this lock
                // or its own locking handled. Assuming _comicService.GetAllComics() returns a safe copy or is thread-safe.
                var allComics = _comicService.GetAllComics();
                if (allComics.Any(c => c.IsRented && c.RentedToMemberId == id))
                {
                    _logger.LogWarning($"已阻止刪除擁有有效租借紀錄的會員ID {id} ('{memberToRemove.Name}')。");
                    throw new InvalidOperationException("無法刪除會員: 會員擁有有效的漫畫租借紀錄。");
                }

                memberName = memberToRemove.Name;
                removed = _members.Remove(memberToRemove);
                if(removed)
                {
                    _logger.Log($"會員 '{memberName}' (ID: {id}) 已從記憶體列表移除。會員總數: {_members.Count}。");
                }
            }

            if(removed)
            {
                await SaveMembersAsync();
            }
        }

        // Renamed from GetNextId to GetNextIdInternal to signify it should be called carefully (e.g. within a lock)
        private int GetNextIdInternal()
        {
            // This method is called from AddMemberAsync, which already holds _membersLock
            int nextId = !_members.Any() ? 1 : _members.Max(m => m.Id) + 1;
            _logger.Log($"下一個可用的會員ID已確定為: {nextId}。");
            return nextId;
        }

        public Member? GetMemberByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.Log("已呼叫 GetMemberByName，姓名為空。");
                return null;
            }
            _logger.Log($"已為姓名: '{name}' 呼叫 GetMemberByName。");
            Member? member;
            lock(_membersLock)
            {
                member = _members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            if (member == null)
            {
                _logger.Log($"找不到姓名為: '{name}' 的會員。");
            }
            else
            {
                _logger.Log($"找到姓名為: '{name}' 的會員: ID='{member.Id}'。");
            }
            return member;
        }

        public Member? GetMemberByPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                _logger.Log("已呼叫 GetMemberByPhoneNumber，電話號碼為空。");
                return null;
            }
            _logger.Log($"已為電話號碼: '{phoneNumber}' 呼叫 GetMemberByPhoneNumber。");
            Member? member;
            lock(_membersLock)
            {
                member = _members.FirstOrDefault(m => m.PhoneNumber.Equals(phoneNumber));
            }
            if (member == null)
            {
                _logger.Log($"找不到電話號碼為: '{phoneNumber}' 的會員。");
            }
            else
            {
                _logger.Log($"找到電話號碼為: '{phoneNumber}' 的會員: ID='{member.Id}', 姓名='{member.Name}'。");
            }
            return member;
        }

        public Member? GetMemberByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("已呼叫 GetMemberByUsername，使用者名稱為空或空白。");
                return null;
            }

            var allMembers = GetAllMembers();

            if (allMembers == null || !allMembers.Any())
            {
                _logger.LogWarning("GetMemberByUsername: 無可用會員進行搜尋。");
                return null;
            }

            Member? foundMember = allMembers.FirstOrDefault(m =>
                string.Equals(m.Username, username, StringComparison.OrdinalIgnoreCase)
            );

            if (foundMember != null)
            {
                _logger.Log($"GetMemberByUsername: 找到ID為 {foundMember.Id} 且使用者名稱為 '{username}' 的會員。");
            }
            else
            {
                _logger.Log($"GetMemberByUsername: 找不到使用者名稱為 '{username}' 的會員。");
            }

            return foundMember;
        }

        public List<Member> SearchMembers(string searchTerm)
        {
            _logger.Log($"已呼叫 SearchMembers，搜尋詞: '{searchTerm}'。");
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<Member>(_members); 
            }

            var lowerSearchTerm = searchTerm.ToLowerInvariant();
            List<Member> currentMembers;
            lock(_membersLock)
            {
                currentMembers = new List<Member>(_members);
            }
            List<Member> results = currentMembers.Where(m =>
                (m.Name != null && m.Name.ToLowerInvariant().Contains(lowerSearchTerm)) ||
                (m.PhoneNumber != null && m.PhoneNumber.Contains(searchTerm)) ||
                (m.Id.ToString().Equals(searchTerm)) ||
                (m.Username != null && m.Username.ToLowerInvariant().Contains(lowerSearchTerm)) 
            ).ToList();

            _logger.Log($"SearchMembers 找到 {results.Count} 位符合條件的會員。");
            return results; // This is already a new list, no need to wrap again
        }
    }
}