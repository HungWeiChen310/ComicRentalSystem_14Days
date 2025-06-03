using System;
using System.Collections.Generic;
using System.IO; 
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Forms; 
using ComicRentalSystem_14Days.Helpers;
using ComicRentalSystem_14Days.Interfaces;
using ComicRentalSystem_14Days.Models;

namespace ComicRentalSystem_14Days.Services
{
    public class ComicService
    {
        private readonly IFileHelper _fileHelper;
        private readonly string _comicFileName = Constants.FileNames.Comics; // Replaced
        private List<Comic> _comics = new List<Comic>();
        private readonly ILogger _logger;
        private readonly object _comicsLock = new object();

        public delegate void ComicDataChangedEventHandler(object? sender, EventArgs e);
        public event ComicDataChangedEventHandler? ComicsChanged;

        public static async Task<ComicService> CreateAsync(IFileHelper fileHelper, ILogger logger)
        {
            var service = new ComicService(fileHelper, logger);
            await service.ReloadAsync(); // Ensure comics are loaded on creation
            return service;
        }

        public async Task ReloadAsync()
        {
            _logger.Log("ComicService 要求非同步重新載入。");
            List<Comic> loadedComics = await InternalLoadComicsAsync();
            lock (_comicsLock)
            {
                _comics = loadedComics;
            }
            OnComicsChanged();
            _logger.Log($"ComicService 已非同步重新載入。已載入 {_comics.Count} 本漫畫。");
        }

        private ComicService(IFileHelper fileHelper, ILogger? logger)
        {
            _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "ComicService 的記錄器不可為空。");

            _logger.Log("ComicService 初始化中。");

            // Synchronous load during construction is kept for now,
            // but CreateAsync is the preferred way to instantiate and load.
            LoadComicsFromFile(); 
            _logger.Log($"ComicService 初始化完成。已載入 {_comics.Count} 本漫畫。");
        }

        private void LoadComicsFromFile()
        {
            _logger.Log($"正在嘗試從檔案載入漫畫 (同步): '{_comicFileName}'。");
            lock (_comicsLock)
            {
                _logger.Log($"LoadComicsFromFile [Before Read]: Attempting to read from file '{_comicFileName}'. Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'.");
                try
                {
                    // Reading the raw string synchronously.
                    string csvData = _fileHelper.ReadFile(_comicFileName);
                    _comics = ParseComicsFromCsv(csvData);
                    _logger.Log($"LoadComicsFromFile [After Read]: Successfully read and parsed. Loaded {_comics.Count} comics from '{_comicFileName}'.");
                }
                catch (Exception ex) when (ex is FormatException || ex is IOException)
                {
                    _logger.LogError($"嚴重錯誤: 漫畫資料檔案 '{_comicFileName}' (同步) 已損壞或無法讀取。Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'. 詳細資訊: {ex.Message}", ex);
                    throw new ApplicationException($"無法從 '{_comicFileName}' (同步) 載入漫畫資料。應用程式可能無法正常運作。", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"從 '{_comicFileName}' (同步) 載入漫畫時發生未預期的錯誤。Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'. 詳細資訊: {ex.Message}", ex);
                    throw new ApplicationException("載入漫畫資料期間 (同步) 發生未預期錯誤。", ex);
                }
            }
        }

        private List<Comic> ParseComicsFromCsv(string csvData)
        {
            var comicsList = new List<Comic>();
            if (string.IsNullOrWhiteSpace(csvData))
            {
                _logger.Log($"CSV 資料為空或僅包含空白字元。路徑: '{_fileHelper.GetFullFilePath(_comicFileName)}'.");
                return comicsList;
            }

            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    comicsList.Add(Comic.FromCsvString(line));
                }
                catch (FormatException formatEx)
                {
                    // Log with line number (i+1 because lines are 1-indexed for human readability)
                    _logger.LogError($"解析行失敗 (行號: {i + 1}) for file '{_comicFileName}' (Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'): '{line}'. 錯誤: {formatEx.Message}", formatEx);
                }
            }
            return comicsList;
        }

        private async Task<List<Comic>> InternalLoadComicsAsync()
        {
            _logger.Log($"正在嘗試從檔案非同步載入漫畫: '{_comicFileName}'。");
            try
            {
                _logger.Log($"InternalLoadComicsAsync [Before Read]: Attempting to read from file '{_comicFileName}'. Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'.");
                string csvData = await _fileHelper.ReadFileAsync(_comicFileName);
                _logger.Log($"InternalLoadComicsAsync [Raw Data]: Read from '{_comicFileName}'. Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'. Data: '{csvData.Replace("\r", "\\r").Replace("\n", "\\n")}'.");

                var loadedComics = ParseComicsFromCsv(csvData);
                _logger.Log($"成功從 '{_comicFileName}' (非同步) 載入並解析 {loadedComics.Count} 本漫畫。");
                return loadedComics;
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning($"漫畫檔案 '{_comicFileName}' (非同步) 找不到。Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'. 返回空列表。");
                return new List<Comic>();
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"讀取漫畫檔案 '{_comicFileName}' (非同步) 時發生IO錯誤: {ioEx.Message}. Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'.", ioEx);
                return new List<Comic>(); 
            }
            catch (Exception ex)
            {
                _logger.LogError($"從 '{_comicFileName}' (非同步) 載入漫畫時發生未預期的錯誤: {ex.Message}. Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'.", ex);
                return new List<Comic>();
            }
        }

        // Made private, will be replaced by SaveComicsAsync for primary use
        private void SaveComics()
        {
            _logger.Log($"[同步儲存] 正在嘗試將 {_comics.Count} 本漫畫儲存到檔案: '{_comicFileName}'。");
            lock (_comicsLock)
            {
                _logger.Log($"SaveComics (sync) [Before Write]: Preparing to write {_comics.Count} comics to file '{_comicFileName}'. Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'.");
                try
                {
                    _fileHelper.WriteFile<Comic>(_comicFileName, _comics, comic => comic.ToCsvString());
                    _logger.Log($"SaveComics (sync) [After Write]: Successfully called _fileHelper.WriteFile for '{_comicFileName}' with {_comics.Count} comics.");
                    OnComicsChanged(); // Keep sync OnComicsChanged if this path is ever used, though it shouldn't be primary.
                    _logger.Log($"[同步儲存] 已成功將 {_comics.Count} 本漫畫儲存到 '{_comicFileName}'。");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[同步儲存] 將 {(_comics != null ? _comics.Count : 0)} 本漫畫儲存到 '{_comicFileName}' 時發生錯誤。 Full path: '{_fileHelper.GetFullFilePath(_comicFileName)}'.", ex);
                    throw; // Rethrow to indicate failure of sync save if it was critically called.
                }
            }
        }

        public async Task SaveComicsAsync()
        {
            int comicsCount = 0;
            string fullPath = string.Empty;
            try
            {
                List<Comic> comicsToSave;
                lock (_comicsLock) // Lock only for reading _comics list to avoid issues if another thread modifies it
                {
                    // Create a copy of the list to save, to avoid holding the lock during I/O
                    comicsToSave = new List<Comic>(_comics);
                }
                comicsCount = comicsToSave.Count;
                fullPath = _fileHelper.GetFullFilePath(_comicFileName);

                _logger.Log($"[非同步儲存] 正在嘗試將 {comicsCount} 本漫畫儲存到檔案: '{_comicFileName}'. Full path: '{fullPath}'.");

                await _fileHelper.WriteFileAsync<Comic>(_comicFileName, comicsToSave, comic => comic.ToCsvString());

                _logger.Log($"[非同步儲存] 已成功將 {comicsCount} 本漫畫儲存到 '{_comicFileName}'. Full path: '{fullPath}'.");
                OnComicsChanged(); // This will notify UI or other listeners
            }
            catch (Exception ex)
            {
                _logger.LogError($"[非同步儲存] 將 {comicsCount} 本漫畫儲存到 '{_comicFileName}' (Full path: '{fullPath}') 時發生錯誤。", ex);
                // Depending on requirements, you might want to rethrow or handle more gracefully
                // For now, logging the error is the primary action. If rethrowing, ensure callers handle it.
                throw;
            }
        }

        protected virtual void OnComicsChanged()
        {
            ComicsChanged?.Invoke(this, EventArgs.Empty);
            _logger.Log("已觸發 ComicsChanged 事件。");
        }

        public List<Comic> GetAllComics()
        {
            _logger.Log("已呼叫 GetAllComics。");
            lock (_comicsLock)
            {
                return new List<Comic>(_comics);
            }
        }

        public Comic? GetComicById(int id)
        {
            _logger.Log($"已為ID: {id} 呼叫 GetComicById。");
            Comic? comic = _comics.FirstOrDefault(c => c.Id == id);
            if (comic == null)
            {
                _logger.Log($"找不到ID為: {id} 的漫畫。");
            }
            else
            {
                _logger.Log($"找到ID為: {id} 的漫畫: 書名='{comic.Title}'。");
            }
            return comic;
        }

        public async Task AddComicAsync(Comic comic)
        {
            if (comic == null)
            {
                var ex = new ArgumentNullException(nameof(comic));
                _logger.LogError("嘗試新增空的漫畫物件。", ex);
                throw ex;
            }

            _logger.Log($"正在嘗試新增漫畫: 書名='{comic.Title}', 作者='{comic.Author}'。");

            lock (_comicsLock) // Lock for modifying _comics collection and GetNextIdInternal
            {
                if (comic.Id != 0 && _comics.Any(c => c.Id == comic.Id))
                {
                    var ex = new InvalidOperationException($"ID為 {comic.Id} 的漫畫已存在。");
                    _logger.LogError($"新增漫畫失敗: ID {comic.Id} (書名='{comic.Title}') 已存在。", ex);
                    throw ex;
                }
                if (_comics.Any(c => c.Title.Equals(comic.Title, StringComparison.OrdinalIgnoreCase) &&
                                     c.Author.Equals(comic.Author, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning($"書名='{comic.Title}' 且作者='{comic.Author}' 相同的漫畫已存在。繼續新增。");
                }

                if (comic.Id == 0)
                {
                    comic.Id = GetNextIdInternal(); // Assumes GetNextIdInternal is safe within this lock
                    _logger.Log($"已為漫畫 '{comic.Title}' 產生新的ID {comic.Id}。");
                }

                _comics.Add(comic);
                _logger.Log($"漫畫 '{comic.Title}' (ID: {comic.Id}) 已新增至記憶體列表。漫畫總數: {_comics.Count}。");
            }
            await SaveComicsAsync();
        }

        public async Task UpdateComicAsync(Comic comic)
        {
            if (comic == null)
            {
                var ex = new ArgumentNullException(nameof(comic));
                _logger.LogError("嘗試使用空的漫畫物件進行更新。", ex);
                throw ex;
            }

            _logger.Log($"正在嘗試更新ID為: {comic.Id} (書名='{comic.Title}') 的漫畫。");
            bool needsSave = false;
            lock (_comicsLock) // Lock for finding and updating item in _comics collection
            {
                Comic? existingComic = _comics.FirstOrDefault(c => c.Id == comic.Id);
                if (existingComic == null)
                {
                    var ex = new InvalidOperationException($"找不到ID為 {comic.Id} 的漫畫進行更新。");
                    _logger.LogError($"更新漫畫失敗: 找不到ID {comic.Id} (書名='{comic.Title}')。", ex);
                    throw ex;
                }

                // Check if any property actually changed to avoid unnecessary save
                if (existingComic.Title != comic.Title || existingComic.Author != comic.Author ||
                    existingComic.Isbn != comic.Isbn || existingComic.Genre != comic.Genre ||
                    existingComic.IsRented != comic.IsRented || existingComic.RentedToMemberId != comic.RentedToMemberId ||
                    existingComic.RentalDate != comic.RentalDate || existingComic.ReturnDate != comic.ReturnDate ||
                    existingComic.ActualReturnTime != comic.ActualReturnTime)
                {
                    existingComic.Title = comic.Title;
                    existingComic.Author = comic.Author;
                    existingComic.Isbn = comic.Isbn;
                    existingComic.Genre = comic.Genre;
                    existingComic.IsRented = comic.IsRented;
                    existingComic.RentedToMemberId = comic.RentedToMemberId;
                    existingComic.RentalDate = comic.RentalDate;
                    existingComic.ReturnDate = comic.ReturnDate;
                    existingComic.ActualReturnTime = comic.ActualReturnTime;
                    _logger.Log($"ID {comic.Id} (書名='{existingComic.Title}') 的漫畫屬性已在記憶體中更新。");
                    needsSave = true;
                }
                else
                {
                    _logger.Log($"ID {comic.Id} (書名='{existingComic.Title}') 的漫畫屬性未變更。略過儲存。");
                }
            }

            if (needsSave)
            {
                await SaveComicsAsync();
                _logger.Log($"ID為: {comic.Id} 的漫畫更新已請求非同步保存。");
            }
        }

        public async Task DeleteComicAsync(int id)
        {
            _logger.Log($"正在嘗試刪除ID為: {id} 的漫畫。");
            bool removed = false;
            string? comicTitle = null;
            lock (_comicsLock) // Lock for finding and removing item from _comics collection
            {
                Comic? comicToRemove = _comics.FirstOrDefault(c => c.Id == id);

                if (comicToRemove == null)
                {
                    var ex = new InvalidOperationException($"找不到ID為 {id} 的漫畫進行刪除。");
                    _logger.LogError($"刪除漫畫失敗: 找不到ID {id}。", ex);
                    throw ex;
                }

                if (comicToRemove.IsRented)
                {
                    _logger.LogWarning($"已阻止刪除已租借的漫畫ID {id} ('{comicToRemove.Title}')。由會員ID: {comicToRemove.RentedToMemberId} 租借。");
                    throw new InvalidOperationException("無法刪除漫畫: 漫畫目前已租借。");
                }
                comicTitle = comicToRemove.Title;
                removed = _comics.Remove(comicToRemove);
                if(removed)
                {
                    _logger.Log($"漫畫 '{comicTitle}' (ID: {id}) 已從記憶體列表移除。漫畫總數: {_comics.Count}。");
                }
            }

            if(removed)
            {
                await SaveComicsAsync();
            }
        }

        private int GetNextIdInternal() // This method is called within a lock in AddComicAsync
        {
            int nextId = !_comics.Any() ? 1 : _comics.Max(c => c.Id) + 1;
            _logger.Log($"下一個可用的漫畫ID已確定為: {nextId}。");
            return nextId;
        }

        public List<Comic> GetComicsByGenre(string genreFilter)
        {
            if (string.IsNullOrWhiteSpace(genreFilter))
            {
                _logger.Log("已呼叫 GetComicsByGenre，類型過濾器為空，返回所有漫畫。");
                return new List<Comic>(_comics);
            }
            else
            {
                _logger.Log($"已呼叫 GetComicsByGenre，依類型篩選: '{genreFilter}'。");
                List<Comic> filteredComics = _comics.Where(c => c.Genre.Equals(genreFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                _logger.Log($"找到 {filteredComics.Count} 本類型為 '{genreFilter}' 的漫畫。");
                return filteredComics;
            }
        }

        public List<Comic> SearchComics(string? searchTerm = null) 
        {
            _logger.Log($"已呼叫 SearchComics，搜尋詞: '{searchTerm ?? "N/A"}'。");
            var query = _comics.AsQueryable();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.Log("搜尋詞為空，返回所有漫畫。");
                return _comics.ToList();
            }

            query = query.Where(c =>
                (c.Title != null && c.Title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Author != null && c.Author.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Isbn != null && c.Isbn.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Genre != null && c.Genre.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (c.Id.ToString().Equals(searchTerm))
            );
            _logger.Log($"已套用搜尋詞: '{searchTerm}'。");

            List<Comic> results = query.ToList();
            _logger.Log($"SearchComics 找到 {results.Count} 本相符的漫畫。");
            return results;
        }

        public List<AdminComicStatusViewModel> GetAdminComicStatusViewModels(IEnumerable<Member> allMembers)
        {
            _logger.Log("正在產生 AdminComicStatusViewModels，使用提供的會員列表進行查詢。");
            var allComics = this.GetAllComics(); 
            var memberLookup = allMembers.ToDictionary(m => m.Id);
            var comicStatuses = new List<AdminComicStatusViewModel>();

            foreach (var comic in allComics)
            {
                var viewModel = new AdminComicStatusViewModel
                {
                    Id = comic.Id,
                    Title = comic.Title,
                    Author = comic.Author,
                    Genre = comic.Genre,
                    Isbn = comic.Isbn,
                    RentalDate = comic.RentalDate,
                    ReturnDate = comic.ReturnDate,
                    ActualReturnTime = comic.ActualReturnTime 
                };

                if (comic.IsRented && comic.RentedToMemberId != 0)
                {
                    viewModel.Status = Constants.ComicStatuses.Rented; // Replaced
                    if (memberLookup.TryGetValue(comic.RentedToMemberId, out Member? borrower))
                    {
                        viewModel.BorrowerName = borrower.Name;
                        viewModel.BorrowerPhoneNumber = borrower.PhoneNumber;
                    }
                    else
                    {
                        viewModel.BorrowerName = "不明";
                        viewModel.BorrowerPhoneNumber = "不明";
                        _logger.LogWarning($"在提供的列表中找不到ID為 {comic.RentedToMemberId} 的會員 (對應已租借的漫畫ID {comic.Id})");
                    }
                }
                else
                {
                    viewModel.Status = "在館中";
                }
                comicStatuses.Add(viewModel);
            }
            _logger.Log($"已產生 {comicStatuses.Count} 個 AdminComicStatusViewModels。");
            return comicStatuses;
        }
    }
}   