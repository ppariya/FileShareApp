using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FileSharingApi
{
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "file" or "folder"
        public long Size { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public class CreateFolderRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ParentFolder { get; set; }
    }

    [ApiController]
    [Route("files")]
    public class FileController : ControllerBase
    {
        // Static dictionary for per-file locks
        private static readonly ConcurrentDictionary<string, object> _fileLocks = new ConcurrentDictionary<string, object>();
        private readonly string _storagePath;

        public FileController(IConfiguration config)
        {
            _storagePath = config["StoragePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Storage");
            Directory.CreateDirectory(_storagePath);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile()
        {
            if (!Request.HasFormContentType)
                return BadRequest("Content-Type must be multipart/form-data");

            IFormCollection? form = await TryReadFormAsync();
            if (form == null)
                return BadRequest("No file uploaded");
            if (form.Files.Count == 0)
                return BadRequest("No file uploaded");
            if (form.Files.Count > 1)
                return BadRequest("You can upload only 1 file at a time.");

            const long maxFileSize = 100 * 1024 * 1024; // 100MB
            IFormFile file = form.Files[0];
            string originalName = file.FileName;
            string? newName = form.TryGetValue("newName", out var newNameVal) ? newNameVal.ToString() : null;
            string? folderPath = form.TryGetValue("folderPath", out var folderVal) ? folderVal.ToString() : null;
            string useName = !string.IsNullOrWhiteSpace(newName) ? newName : originalName;

            var nameValidation = ValidateFileName(useName);
            if (nameValidation != null)
                return BadRequest(nameValidation);

            var folderValidation = ValidateFolderPath(folderPath);
            if (folderValidation != null)
                return BadRequest(folderValidation);

            if (file.Length == 0)
                return BadRequest("File is empty.");
            if (file.Length > maxFileSize)
                return BadRequest($"File '{useName}' exceeds the 100MB size limit.");

            string targetDirectory = string.IsNullOrWhiteSpace(folderPath) 
                ? _storagePath 
                : Path.Combine(_storagePath, folderPath.Replace('/', Path.DirectorySeparatorChar));
            
            Directory.CreateDirectory(targetDirectory);
            
            // Auto-rename if file already exists
            string finalFileName = GetUniqueFileName(targetDirectory, useName);
            string filePath = Path.Combine(targetDirectory, finalFileName);

            object fileLock = _fileLocks.GetOrAdd(filePath, _ => new object());
            try
            {
                lock (fileLock)
                {
                    using Stream input = file.OpenReadStream();
                    using FileStream fileStream = System.IO.File.Create(filePath);
                    input.CopyTo(fileStream);
                    fileStream.Flush();
                }
            }
            catch
            {
                // Clean up partial file if error
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
                throw;
            }
            return Ok(new { file = finalFileName, folder = folderPath });
        }

        // Helper: Try to read form, return null if no file or malformed
        private async Task<IFormCollection?> TryReadFormAsync()
        {
            try
            {
                return await Request.ReadFormAsync();
            }
            catch (BadHttpRequestException ex) when (ex.Message.Contains("Request body too large", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            catch (InvalidDataException ex) when (ex.Message.Contains("Request body too large", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        // Helper: Validate filename, return error string or null if valid
        private string? ValidateFileName(string name)
        {
            string safeName = Path.GetFileName(name);
            if (string.IsNullOrWhiteSpace(safeName) || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || safeName.Contains("..") || safeName.Contains("/") || safeName.Contains("\\") || safeName != name)
                return "Invalid file name.";
            if (safeName.Length > 255)
                return "File name too long.";
            return null;
        }

        // Helper: Validate folder path, return error string or null if valid
        private string? ValidateFolderPath(string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return null;

            // Normalize path separators to forward slashes
            folderPath = folderPath.Replace('\\', '/');
            
            // Check for invalid patterns
            if (folderPath.Contains("..") || folderPath.StartsWith("/") || folderPath.EndsWith("/"))
                return "Invalid folder path.";

            var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return "Invalid folder name in path.";
                if (part.Length > 255)
                    return "Folder name too long.";
            }

            if (folderPath.Length > 1000) // Reasonable total path length limit
                return "Folder path too long.";

            return null;
        }

        // Helper: Generate unique filename by adding (1), (2), etc. if file exists
        private string GetUniqueFileName(string directory, string fileName)
        {
            string filePath = Path.Combine(directory, fileName);
            if (!System.IO.File.Exists(filePath))
                return fileName;

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int counter = 1;

            do
            {
                string newFileName = $"{nameWithoutExtension} ({counter}){extension}";
                filePath = Path.Combine(directory, newFileName);
                if (!System.IO.File.Exists(filePath))
                    return newFileName;
                counter++;
            } while (counter < 1000); // Prevent infinite loop

            // Fallback with timestamp if we somehow get to 1000 duplicates
            string timestampFileName = $"{nameWithoutExtension} ({DateTime.Now:yyyyMMdd_HHmmss}){extension}";
            return timestampFileName;
        }

        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] string filename, [FromQuery] string? folder)
        {
            var folderValidation = ValidateFolderPath(folder);
            if (folderValidation != null)
                return BadRequest(folderValidation);

            var nameValidation = ValidateFileName(filename);
            if (nameValidation != null)
                return BadRequest(nameValidation);

            string targetDirectory = string.IsNullOrWhiteSpace(folder) 
                ? _storagePath 
                : Path.Combine(_storagePath, folder.Replace('/', Path.DirectorySeparatorChar));

            string filePath = Path.Combine(targetDirectory, filename);
            if (!System.IO.File.Exists(filePath))
                return NotFound();
            
            object fileLock = _fileLocks.GetOrAdd(filePath, _ => new object());
            lock (fileLock)
            {
                FileStream fileStream = System.IO.File.OpenRead(filePath);
                string contentType = "application/octet-stream";
                return File(fileStream, contentType, fileDownloadName: filename);
            }
        }

        [HttpGet]
        public IActionResult ListFiles([FromQuery] string? search, [FromQuery] string? folder)
        {
            var folderValidation = ValidateFolderPath(folder);
            if (folderValidation != null)
                return BadRequest(folderValidation);

            string targetDirectory = string.IsNullOrWhiteSpace(folder) 
                ? _storagePath 
                : Path.Combine(_storagePath, folder.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(targetDirectory))
                return NotFound("Folder not found");

            List<FileItem> items = new List<FileItem>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Recursive search when search term is provided
                SearchRecursively(targetDirectory, search, items, folder);
            }
            else
            {
                // Regular directory listing when no search term
                // Add folders first
                foreach (string dir in Directory.GetDirectories(targetDirectory))
                {
                    string name = Path.GetFileName(dir);
                    var dirInfo = new DirectoryInfo(dir);
                    items.Add(new FileItem
                    {
                        Name = name,
                        Type = "folder",
                        Size = 0,
                        ModifiedDate = dirInfo.LastWriteTime
                    });
                }

                // Add files
                foreach (string file in Directory.GetFiles(targetDirectory))
                {
                    string name = Path.GetFileName(file);
                    var fileInfo = new FileInfo(file);
                    items.Add(new FileItem
                    {
                        Name = name,
                        Type = "file",
                        Size = fileInfo.Length,
                        ModifiedDate = fileInfo.LastWriteTime
                    });
                }
            }

            // Sort: folders first, then files, both alphabetically
            var sorted = items.OrderBy(i => i.Type == "file" ? 1 : 0).ThenBy(i => i.Name).ToList();
            
            return Ok(new { 
                items = sorted,
                currentFolder = folder ?? "",
                parentFolder = GetParentFolder(folder)
            });
        }

        private string? GetParentFolder(string? currentFolder)
        {
            if (string.IsNullOrWhiteSpace(currentFolder))
                return null;

            var parts = currentFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
                return "";

            return string.Join("/", parts.Take(parts.Length - 1));
        }

        private void SearchRecursively(string currentPath, string searchTerm, List<FileItem> items, string? baseFolderPath)
        {
            try
            {
                // Search files in current directory
                foreach (string file in Directory.GetFiles(currentPath))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        var fileInfo = new FileInfo(file);
                        string relativePath = GetRelativePath(file, baseFolderPath);
                        items.Add(new FileItem
                        {
                            Name = relativePath,
                            Type = "file",
                            Size = fileInfo.Length,
                            ModifiedDate = fileInfo.LastWriteTime
                        });
                    }
                }

                // Search folders in current directory
                foreach (string dir in Directory.GetDirectories(currentPath))
                {
                    string folderName = Path.GetFileName(dir);
                    if (folderName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        string relativePath = GetRelativePath(dir, baseFolderPath);
                        items.Add(new FileItem
                        {
                            Name = relativePath,
                            Type = "folder",
                            Size = 0,
                            ModifiedDate = dirInfo.LastWriteTime
                        });
                    }

                    // Recursively search subdirectories
                    SearchRecursively(dir, searchTerm, items, baseFolderPath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (DirectoryNotFoundException)
            {
                // Skip directories that no longer exist
            }
        }

        private string GetRelativePath(string fullPath, string? baseFolderPath)
        {
            string baseDirectory = string.IsNullOrWhiteSpace(baseFolderPath)
                ? _storagePath
                : Path.Combine(_storagePath, baseFolderPath.Replace('/', Path.DirectorySeparatorChar));

            string relativePath = Path.GetRelativePath(baseDirectory, fullPath);
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        [HttpPost("folder")]
        public IActionResult CreateFolder([FromBody] CreateFolderRequest request)
        {
            var folderValidation = ValidateFolderPath(request.ParentFolder);
            if (folderValidation != null)
                return BadRequest(folderValidation);

            var nameValidation = ValidateFileName(request.Name);
            if (nameValidation != null)
                return BadRequest(nameValidation);

            string parentDirectory = string.IsNullOrWhiteSpace(request.ParentFolder) 
                ? _storagePath 
                : Path.Combine(_storagePath, request.ParentFolder.Replace('/', Path.DirectorySeparatorChar));

            string newFolderPath = Path.Combine(parentDirectory, request.Name);
            
            if (Directory.Exists(newFolderPath))
                return Conflict($"A folder named '{request.Name}' already exists.");

            if (System.IO.File.Exists(newFolderPath))
                return Conflict($"A file named '{request.Name}' already exists.");

            try
            {
                Directory.CreateDirectory(newFolderPath);
                string relativePath = string.IsNullOrWhiteSpace(request.ParentFolder) 
                    ? request.Name 
                    : $"{request.ParentFolder}/{request.Name}";
                return Ok(new { folder = relativePath });
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to create folder: {ex.Message}");
            }
        }

        [HttpDelete("folder")]
        public IActionResult DeleteFolder([FromQuery] string folder, [FromQuery] bool force = false)
        {
            var folderValidation = ValidateFolderPath(folder);
            if (folderValidation != null)
                return BadRequest(folderValidation);

            if (string.IsNullOrWhiteSpace(folder))
                return BadRequest("Cannot delete root folder");

            string folderPath = Path.Combine(_storagePath, folder.Replace('/', Path.DirectorySeparatorChar));
            
            if (!Directory.Exists(folderPath))
                return NotFound("Folder not found");

            try
            {
                // Check if folder is empty (unless force delete is requested)
                if (!force)
                {
                    var files = Directory.GetFiles(folderPath);
                    var subDirectories = Directory.GetDirectories(folderPath);
                    
                    if (files.Length > 0 || subDirectories.Length > 0)
                    {
                        return BadRequest(new { 
                            message = "Folder is not empty", 
                            isEmpty = false,
                            filesCount = files.Length,
                            foldersCount = subDirectories.Length
                        });
                    }
                }

                Directory.Delete(folderPath, recursive: true);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to delete folder: {ex.Message}");
            }
        }

        [HttpDelete("file")]
        public IActionResult DeleteFile([FromQuery] string filename, [FromQuery] string? folder)
        {
            var folderValidation = ValidateFolderPath(folder);
            if (folderValidation != null)
                return BadRequest(folderValidation);

            var nameValidation = ValidateFileName(filename);
            if (nameValidation != null)
                return BadRequest(nameValidation);

            string targetDirectory = string.IsNullOrWhiteSpace(folder) 
                ? _storagePath 
                : Path.Combine(_storagePath, folder.Replace('/', Path.DirectorySeparatorChar));

            string filePath = Path.Combine(targetDirectory, filename);
            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found");

            object fileLock = _fileLocks.GetOrAdd(filePath, _ => new object());
            lock (fileLock)
            {
                System.IO.File.Delete(filePath);
            }

            // Optionally remove lock after delete
            _fileLocks.TryRemove(filePath, out _);
            return NoContent();
        }
    }
}
