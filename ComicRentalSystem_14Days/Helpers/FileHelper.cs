using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ComicRentalSystem_14Days.Interfaces; 

namespace ComicRentalSystem_14Days.Helpers
{
    public class FileHelper : IFileHelper 
    {
        private readonly string _baseDataPath; 
        private readonly ILogger _logger;

        public FileHelper(ILogger logger, string baseDataFolderName = "AppData")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ComicRentalApp", baseDataFolderName);

            try
            {
                if (!Directory.Exists(_baseDataPath))
                {
                    _logger.Log($"Base data directory not found. Creating: {_baseDataPath}");
                    Directory.CreateDirectory(_baseDataPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"建立資料目錄 '{_baseDataPath}' 時發生錯誤: {ex.Message}", ex);
                // Console.WriteLine($"建立資料目錄 '{_baseDataPath}' 時發生錯誤: {ex.Message}"); // Original line
                throw new IOException($"無法建立或存取資料目錄: {_baseDataPath}", ex);
            }
        }

        public string GetFullFilePath(string fileName)
        {
            return Path.Combine(_baseDataPath, fileName);
        }

        public string ReadFile(string fileName)
        {
            string filePath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to read file: {filePath}");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"File not found during read attempt: {filePath}");
                return string.Empty;
            }
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                _logger.Log($"Successfully read file: {filePath}");
                return content;
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error reading file {filePath}.", ioEx);
                // Console.WriteLine($"讀取檔案 '{filePath}' 時發生錯誤: {ioEx.Message}"); // Original line
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error reading file {filePath}.", ex);
                // Console.WriteLine($"讀取 '{filePath}' 時發生未預期的錯誤: {ex.Message}"); // Original line
                throw;
            }
        }

        public void WriteFile(string fileName, string content)
        {
            string filePath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to write file: {filePath}");
            try
            {
                File.WriteAllText(filePath, content, Encoding.UTF8);
                _logger.Log($"Successfully wrote to file: {filePath}");
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error writing file {filePath}.", ioEx);
                // Console.WriteLine($"寫入檔案 '{filePath}' 時發生錯誤: {ioEx.Message}"); // Original line
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error writing file {filePath}.", ex);
                // Console.WriteLine($"寫入 '{filePath}' 時發生未預期的錯誤: {ex.Message}"); // Original line
                throw;
            }
        }

        public List<T> ReadFile<T>(string fileName, Func<string, T> parseFunc)
        {
            string filePath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to read and parse file: {filePath}");
            List<T> records = new List<T>();

            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"File not found during read and parse attempt: {filePath}");
                return records;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                _logger.Log($"Read {lines.Length} lines from {filePath} for parsing.");
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    records.Add(parseFunc(line));
                }
                _logger.Log($"Successfully parsed {records.Count} records from {filePath}.");
            }
            catch (IOException ioEx) 
            {
                _logger.LogError($"IO error reading and parsing file {filePath}.", ioEx);
                // Console.WriteLine($"讀取檔案 '{filePath}' 時發生錯誤: {ioEx.Message}"); // Original line
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error reading and parsing file {filePath}.", ex);
                // Console.WriteLine($"讀取 '{filePath}' 時發生未預期的錯誤: {ex.Message}"); // Original line
                throw;
            }
            return records;
        }

        public void WriteFile<T>(string fileName, IEnumerable<T> records, Func<T, string> toCsvFunc)
        {
            string filePath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to write generic records to file: {filePath}");
            try
            {
                List<string> lines = new List<string>();
                foreach (var record in records)
                {
                     if (record != null) lines.Add(toCsvFunc(record));
                }
                File.WriteAllLines(filePath, lines, Encoding.UTF8);
                _logger.Log($"Successfully wrote {lines.Count} generic records to file: {filePath}");
            }
            catch (IOException ioEx) 
            {
                _logger.LogError($"IO error writing generic records to file {filePath}.", ioEx);
                // Console.WriteLine($"寫入檔案 '{filePath}' 時發生錯誤: {ioEx.Message}"); // Original line
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error writing generic records to file {filePath}.", ex);
                // Console.WriteLine($"寫入 '{filePath}' 時發生未預期的錯誤: {ex.Message}"); // Original line
                throw;
            }
        }

        public async Task<string> ReadFileAsync(string fileName) // Changed filePath to fileName for consistency
        {
            string fullPath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to read file asynchronously: {fullPath}");
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning($"File not found during async read: {fullPath}");
                return string.Empty;
            }
            try
            {
                string content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
                _logger.Log($"Successfully read file asynchronously: {fullPath}");
                return content;
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error reading file {fullPath} asynchronously.", ioEx);
                // Console.WriteLine($"非同步讀取檔案 '{fullPath}' 時發生錯誤: {ioEx.Message}"); // Original line
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error reading file {fullPath} asynchronously.", ex);
                // Console.WriteLine($"非同步讀取 '{fullPath}' 時發生未預期的錯誤: {ex.Message}"); // Original line
                throw; 
            }
        }

        public async Task WriteFileAsync(string fileName, string content) // Changed filePath to fileName for consistency
        {
            string fullPath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to write file asynchronously: {fullPath}");
            try
            {
                await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
                _logger.Log($"Successfully wrote to file asynchronously: {fullPath}");
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error writing file {fullPath} asynchronously.", ioEx);
                // Console.WriteLine($"非同步寫入檔案 '{fullPath}' 時發生錯誤: {ioEx.Message}"); // Original line
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error writing file {fullPath} asynchronously.", ex);
                // Console.WriteLine($"非同步寫入 '{fullPath}' 時發生未預期的錯誤: {ex.Message}"); // Original line
                throw; 
            }
        }

        public async Task WriteFileAsync<T>(string fileName, IEnumerable<T> data, Func<T, string> formatter)
        {
            string filePath = GetFullFilePath(fileName);
            _logger.Log($"Attempting to write generic data to file asynchronously: {filePath}");
            try
            {
                if (data == null)
                {
                    _logger.LogWarning($"WriteFileAsync<{typeof(T).Name}>: Data provided is null. Writing an empty file to {filePath}.");
                    await System.IO.File.WriteAllLinesAsync(filePath, new List<string>(), System.Text.Encoding.UTF8);
                    return;
                }

                List<string> lines = new List<string>();
                foreach (var item in data)
                {
                    if (item != null)
                    {
                        lines.Add(formatter(item));
                    }
                    else
                    {
                        _logger.LogWarning($"WriteFileAsync<{typeof(T).Name}>: Encountered a null item in the data collection for {filePath}. Skipping this item.");
                    }
                }
                await System.IO.File.WriteAllLinesAsync(filePath, lines, System.Text.Encoding.UTF8);
                _logger.Log($"Successfully wrote {lines.Count} lines of generic data to file: {filePath}");
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error writing generic data to file {filePath} asynchronously.", ioEx);
                throw; // Re-throw to allow calling code to handle
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error writing generic data to file {filePath} asynchronously.", ex);
                throw; // Re-throw to allow calling code to handle
            }
        }

        public bool FileExists(string filePath)
        {
            string fullPath = GetFullFilePath(filePath);
            // _logger.Log($"Checking if file exists: {fullPath}"); // Optional: too verbose for Exists?
            return File.Exists(fullPath);
        }

        public void DeleteFile(string filePath)
        {
            string fullPath = GetFullFilePath(filePath);
            _logger.Log($"Attempting to delete file: {fullPath}");
            try
            {
                File.Delete(fullPath);
                _logger.Log($"Successfully deleted file: {fullPath}");
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error deleting file {fullPath}.", ioEx);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error deleting file {fullPath}.", ex);
                throw;
            }
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            string fullSourcePath = GetFullFilePath(sourcePath);
            string fullDestinationPath = GetFullFilePath(destinationPath);
            _logger.Log($"Attempting to move file from {fullSourcePath} to {fullDestinationPath}");
            try
            {
                File.Move(fullSourcePath, fullDestinationPath);
                _logger.Log($"Successfully moved file from {fullSourcePath} to {fullDestinationPath}");
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error moving file from {fullSourcePath} to {fullDestinationPath}.", ioEx);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error moving file from {fullSourcePath} to {fullDestinationPath}.", ex);
                throw;
            }
        }

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            string fullSourcePath = GetFullFilePath(sourcePath);
            string fullDestinationPath = GetFullFilePath(destinationPath);
            _logger.Log($"Attempting to copy file from {fullSourcePath} to {fullDestinationPath} (overwrite: {overwrite})");
            try
            {
                File.Copy(fullSourcePath, fullDestinationPath, overwrite);
                _logger.Log($"Successfully copied file from {fullSourcePath} to {fullDestinationPath}");
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"IO error copying file from {fullSourcePath} to {fullDestinationPath}.", ioEx);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error copying file from {fullSourcePath} to {fullDestinationPath}.", ex);
                throw;
            }
        }
    }
}
