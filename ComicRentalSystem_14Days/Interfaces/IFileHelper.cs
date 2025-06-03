using System;
using System.Collections.Generic;

namespace ComicRentalSystem_14Days.Interfaces
{
    public interface IFileHelper
    {
        string ReadFile(string fileName); 
        void WriteFile(string fileName, string content); 
        List<T> ReadFile<T>(string fileName, Func<string, T> parser); 
        void WriteFile<T>(string fileName, IEnumerable<T> items, Func<T, string> formatter); 
        string GetFullFilePath(string fileName); 

        Task<string> ReadFileAsync(string filePath);
        Task WriteFileAsync(string filePath, string content);
        Task WriteFileAsync<T>(string fileName, IEnumerable<T> data, Func<T, string> formatter); // Added new async generic method
        bool FileExists(string filePath);
        void DeleteFile(string filePath);
        void MoveFile(string sourcePath, string destinationPath);
        void CopyFile(string sourcePath, string destinationPath, bool overwrite);
            }
        }

        public async Task WriteFileAsync<T>(string fileName, IEnumerable<T> records, Func<T, string> toCsvFunc)
        {
            string filePath = GetFullFilePath(fileName);
            try
            {
                List<string> lines = new List<string>();
                foreach (var record in records)
                {
                    lines.Add(toCsvFunc(record));
                }
                await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"非同步寫入檔案 (generic) '{filePath}' 時發生錯誤: {ioEx.Message}");
                // Consider logging to a proper logger if available, instead of Console.WriteLine
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"非同步寫入 (generic) '{filePath}' 時發生未預期的錯誤: {ex.Message}");
                // Consider logging
                throw;
    }
}
