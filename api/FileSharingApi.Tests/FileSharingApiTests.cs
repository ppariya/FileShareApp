


using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FileSharingApi.Tests;

public class FileSharingApiTests : IClassFixture<WebApplicationFactory<FileSharingApi.Program>>
{

    private readonly WebApplicationFactory<FileSharingApi.Program> _factory;
    private readonly HttpClient _client;

    public FileSharingApiTests(WebApplicationFactory<FileSharingApi.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Optionally override storage path for isolation
                var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);
                config.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("StoragePath", tempDir) });
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task UploadFile_InvalidFileName_ReturnsBadRequest()
    {
        string fileName = "../bad.txt";
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("bad"), "file", fileName);
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid file name", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_EmptyFile_ReturnsBadRequest()
    {
        string fileName = "empty.txt";
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Array.Empty<byte>()), "file", fileName);
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_Duplicate_AutoRenames()
    {
        string fileName = "dupe.txt";
        string content1 = "first file";
        string content2 = "second file";
        
        // Upload first file
        MultipartFormDataContent form1 = new MultipartFormDataContent();
        form1.Add(new StringContent(content1), "file", fileName);
        HttpResponseMessage response1 = await _client.PostAsync("/files/upload", form1, TestContext.Current.CancellationToken);
        response1.EnsureSuccessStatusCode();
        
        // Upload second file with same name - should auto-rename
        MultipartFormDataContent form2 = new MultipartFormDataContent();
        form2.Add(new StringContent(content2), "file", fileName);
        HttpResponseMessage response2 = await _client.PostAsync("/files/upload", form2, TestContext.Current.CancellationToken);
        response2.EnsureSuccessStatusCode();
        
        // Check that second file was auto-renamed
        JsonElement json = await response2.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("file", out JsonElement fileElement));
        string actualFileName = fileElement.GetString()!;
        Assert.Equal("dupe (1).txt", actualFileName);
        
        // Verify both files exist
        HttpResponseMessage listResponse = await _client.GetAsync("/files", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var fileNames = items.Where(item => item.GetProperty("type").GetString() == "file")
                             .Select(item => item.GetProperty("name").GetString())
                             .ToList();
        
        Assert.Contains("dupe.txt", fileNames);
        Assert.Contains("dupe (1).txt", fileNames);
    }

    [Fact]
    public async Task DownloadFile_NotFound_ReturnsNotFound()
    {
        string fileName = "nope.txt";
        HttpResponseMessage response = await _client.GetAsync($"/files/download?filename={fileName}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_TooLongFileName_ReturnsBadRequest()
    {
        string fileName = new string('a', 256) + ".txt";
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("bad"), "file", fileName);
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("too long", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_NoFile_ReturnsBadRequest()
    {
        MultipartFormDataContent form = new MultipartFormDataContent();
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("No file uploaded", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_MultipleFiles_ReturnsBadRequest()
    {
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("a"), "file", "a.txt");
        form.Add(new StringContent("b"), "file", "b.txt");
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("only 1 file", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_TooLarge_ReturnsBadRequestOrInternalServerError()
    {
        // Arrange: create a file just over 100MB
        string fileName = "toolarge.bin";
        byte[] contentBytes = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(contentBytes), "file", fileName);

        // Act
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError });
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("100MB", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_ReturnsOk()
    {
        // Arrange
        string fileName = "test.txt";
        string content = "Hello World";
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(contentBytes), "file", fileName);

        // Act
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("file", out JsonElement fileElement));
        Assert.Equal(fileName, fileElement.GetString());
    }

    [Fact]
    public async Task DownloadFile_ReturnsFile()
    {
        // Arrange: upload a file first
        string fileName = "test.txt";
        string content = "test content";
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent(content), "file", fileName);
        HttpResponseMessage uploadResp = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        uploadResp.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/files/download?filename={fileName}", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
        string downloaded = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(content, downloaded);
    }

    [Fact]
    public async Task ListFiles_ReturnsFiles()
    {
        // Arrange: upload two files
        string file1 = "file1.txt";
        string file2 = "file2.txt";
        MultipartFormDataContent form1 = new MultipartFormDataContent();
        form1.Add(new StringContent("a"), "file", file1);
        MultipartFormDataContent form2 = new MultipartFormDataContent();
        form2.Add(new StringContent("b"), "file", file2);
        await _client.PostAsync("/files/upload", form1, TestContext.Current.CancellationToken);
        await _client.PostAsync("/files/upload", form2, TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/files", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var fileNames = items.Where(item => item.GetProperty("type").GetString() == "file")
                             .Select(item => item.GetProperty("name").GetString())
                             .ToList();
        
        Assert.Contains(file1, fileNames);
        Assert.Contains(file2, fileNames);
    }

    [Fact]
    public async Task ListFiles_Search_ReturnsFiltered()
    {
        // Arrange: upload two files
        string file1 = "file1.txt";
        string file2 = "other.txt";
        MultipartFormDataContent form1 = new MultipartFormDataContent();
        form1.Add(new StringContent("a"), "file", file1);
        MultipartFormDataContent form2 = new MultipartFormDataContent();
        form2.Add(new StringContent("b"), "file", file2);
        await _client.PostAsync("/files/upload", form1, TestContext.Current.CancellationToken);
        await _client.PostAsync("/files/upload", form2, TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/files?search=file1", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var fileNames = items.Where(item => item.GetProperty("type").GetString() == "file")
                             .Select(item => item.GetProperty("name").GetString())
                             .ToList();
        
        Assert.Single(fileNames);
        Assert.Contains(file1, fileNames);
    }

    [Fact]
    public async Task ListFiles_SearchRecursive_ReturnsFilesFromSubfolders()
    {
        // Arrange: create folder and upload files in different locations
        var folderRequest = new { name = "searchtest", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);
        
        // Upload file in root
        MultipartFormDataContent rootForm = new MultipartFormDataContent();
        rootForm.Add(new StringContent("root content"), "file", "searchfile_root.txt");
        await _client.PostAsync("/files/upload", rootForm, TestContext.Current.CancellationToken);
        
        // Upload file in subfolder
        MultipartFormDataContent subForm = new MultipartFormDataContent();
        subForm.Add(new StringContent("sub content"), "file", "searchfile_sub.txt");
        subForm.Add(new StringContent("searchtest"), "folderPath");
        await _client.PostAsync("/files/upload", subForm, TestContext.Current.CancellationToken);
        
        // Upload unrelated file
        MultipartFormDataContent otherForm = new MultipartFormDataContent();
        otherForm.Add(new StringContent("other"), "file", "other.txt");
        await _client.PostAsync("/files/upload", otherForm, TestContext.Current.CancellationToken);

        // Act - search for files containing "searchfile"
        HttpResponseMessage response = await _client.GetAsync("/files?search=searchfile", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var fileNames = items.Where(item => item.GetProperty("type").GetString() == "file")
                             .Select(item => item.GetProperty("name").GetString())
                             .ToList();
        
        Assert.Equal(2, fileNames.Count);
        Assert.Contains("searchfile_root.txt", fileNames);
        Assert.Contains("searchtest/searchfile_sub.txt", fileNames); // Should include relative path
    }

    [Fact]
    public async Task DeleteFile_ReturnsNoContent()
    {
        // Arrange: upload a file
        string fileName = "test.txt";
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("delete me"), "file", fileName);
        await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.DeleteAsync($"/files/file?filename={fileName}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Confirm file is gone by listing
        HttpResponseMessage listResp = await _client.GetAsync("/files", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var fileNames = items.Where(item => item.GetProperty("type").GetString() == "file")
                             .Select(item => item.GetProperty("name").GetString())
                             .ToList();
        Assert.DoesNotContain(fileName, fileNames);
    }

    [Fact]
    public async Task DeleteFile_ReturnsNotFound()
    {
        // Arrange
        string fileName = "notfound.txt";

        // Act
        HttpResponseMessage response = await _client.DeleteAsync($"/files/file?filename={fileName}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #region Folder Tests

    [Fact]
    public async Task CreateFolder_ValidName_ReturnsOk()
    {
        // Arrange
        var request = new { name = "testfolder", parentFolder = (string?)null };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("folder", out JsonElement folderElement));
        Assert.Equal("testfolder", folderElement.GetString());
    }

    [Fact]
    public async Task CreateFolder_InvalidName_ReturnsBadRequest()
    {
        // Arrange
        var request = new { name = "../invalidfolder", parentFolder = (string?)null };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid file name", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFolder_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new { name = "", parentFolder = (string?)null };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid file name", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFolder_TooLongName_ReturnsBadRequest()
    {
        // Arrange
        string longName = new string('a', 256);
        var request = new { name = longName, parentFolder = (string?)null };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("too long", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFolder_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var request = new { name = "duplicate", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Act - try to create same folder again
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("already exists", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFolder_InParentFolder_ReturnsOk()
    {
        // Arrange
        var parentRequest = new { name = "parent", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", parentRequest, TestContext.Current.CancellationToken);
        
        var childRequest = new { name = "child", parentFolder = "parent" };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", childRequest, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("folder", out JsonElement folderElement));
        Assert.Equal("parent/child", folderElement.GetString());
    }

    [Fact]
    public async Task CreateFolder_InvalidParentFolder_ReturnsBadRequest()
    {
        // Arrange
        var request = new { name = "validname", parentFolder = "../invalid" };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid folder path", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteFolder_ExistingFolder_ReturnsNoContent()
    {
        // Arrange
        var request = new { name = "todelete", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", request, TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.DeleteAsync("/files/folder?folder=todelete", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify folder is gone by listing
        HttpResponseMessage listResp = await _client.GetAsync("/files", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var folderNames = items.Where(item => item.GetProperty("type").GetString() == "folder")
                               .Select(item => item.GetProperty("name").GetString())
                               .ToList();
        Assert.DoesNotContain("todelete", folderNames);
    }

    [Fact]
    public async Task DeleteFolder_NonExistentFolder_ReturnsNotFound()
    {
        // Act
        HttpResponseMessage response = await _client.DeleteAsync("/files/folder?folder=nonexistent", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_EmptyFolderPath_ReturnsBadRequest()
    {
        // Act - try to delete with empty folder parameter
        HttpResponseMessage response = await _client.DeleteAsync("/files/folder?folder=", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // The exact error message may vary, but it should be a bad request
    }

    [Fact]
    public async Task DeleteFolder_NoFolderParameter_ReturnsBadRequest()
    {
        // Act - try to delete without specifying folder parameter
        HttpResponseMessage response = await _client.DeleteAsync("/files/folder", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_WithSubfolders_DeletesRecursively()
    {
        // Arrange - create parent/child structure
        var parentRequest = new { name = "parent", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", parentRequest, TestContext.Current.CancellationToken);
        
        var childRequest = new { name = "child", parentFolder = "parent" };
        await _client.PostAsJsonAsync("/files/folder", childRequest, TestContext.Current.CancellationToken);

        // Act - delete parent (should delete child too)
        HttpResponseMessage response = await _client.DeleteAsync("/files/folder?folder=parent", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify both folders are gone
        HttpResponseMessage listResp = await _client.GetAsync("/files", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        var folderNames = items.Where(item => item.GetProperty("type").GetString() == "folder")
                               .Select(item => item.GetProperty("name").GetString())
                               .ToList();
        Assert.DoesNotContain("parent", folderNames);
    }

    [Fact]
    public async Task DeleteFolder_WithFiles_DeletesEverything()
    {
        // Arrange - create folder and add file to it
        var folderRequest = new { name = "folderwithfiles", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);
        
        // Upload file to the folder
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("test content"), "file", "test.txt");
        form.Add(new StringContent("folderwithfiles"), "folderPath");
        await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Act - delete folder
        HttpResponseMessage response = await _client.DeleteAsync("/files/folder?folder=folderwithfiles", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify folder and file are gone
        HttpResponseMessage listResp = await _client.GetAsync("/files", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        Assert.Empty(items); // Should be no items left
    }

    [Fact]
    public async Task ListFiles_WithFolders_ReturnsCorrectTypes()
    {
        // Arrange - create folder and file
        var folderRequest = new { name = "testfolder", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);
        
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("test"), "file", "test.txt");
        await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/files", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        
        Assert.Equal(2, items.Count);
        
        var folder = items.FirstOrDefault(item => item.GetProperty("name").GetString() == "testfolder");
        Assert.True(folder.ValueKind != JsonValueKind.Undefined);
        Assert.Equal("folder", folder.GetProperty("type").GetString());
        
        var file = items.FirstOrDefault(item => item.GetProperty("name").GetString() == "test.txt");
        Assert.True(file.ValueKind != JsonValueKind.Undefined);
        Assert.Equal("file", file.GetProperty("type").GetString());
        Assert.True(file.GetProperty("size").GetInt64() > 0);
    }

    [Fact]
    public async Task ListFiles_InFolder_ReturnsCorrectItems()
    {
        // Arrange - create folder and add files
        var folderRequest = new { name = "listtest", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);
        
        // Add file to folder
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("content"), "file", "inFolder.txt");
        form.Add(new StringContent("listtest"), "folderPath");
        await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);
        
        // Add file to root
        MultipartFormDataContent rootForm = new MultipartFormDataContent();
        rootForm.Add(new StringContent("root content"), "file", "inRoot.txt");
        await _client.PostAsync("/files/upload", rootForm, TestContext.Current.CancellationToken);

        // Act - list files in folder
        HttpResponseMessage response = await _client.GetAsync("/files?folder=listtest", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        
        Assert.Single(items);
        Assert.Equal("inFolder.txt", items[0].GetProperty("name").GetString());
        Assert.Equal("file", items[0].GetProperty("type").GetString());
        
        // Verify currentFolder is correct
        Assert.True(json.TryGetProperty("currentFolder", out JsonElement currentFolderElement));
        Assert.Equal("listtest", currentFolderElement.GetString());
    }

    [Fact]
    public async Task UploadFile_ToFolder_WorksCorrectly()
    {
        // Arrange - create folder
        var folderRequest = new { name = "uploadtest", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);

        // Act - upload file to folder
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("folder content"), "file", "folderfile.txt");
        form.Add(new StringContent("uploadtest"), "folderPath");
        HttpResponseMessage response = await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(json.TryGetProperty("file", out JsonElement fileElement));
        Assert.Equal("folderfile.txt", fileElement.GetString());
        Assert.True(json.TryGetProperty("folder", out JsonElement folderElement));
        Assert.Equal("uploadtest", folderElement.GetString());
        
        // Verify file is in folder
        HttpResponseMessage listResponse = await _client.GetAsync("/files?folder=uploadtest", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        
        Assert.Single(items);
        Assert.Equal("folderfile.txt", items[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task DownloadFile_FromFolder_WorksCorrectly()
    {
        // Arrange - create folder and upload file
        var folderRequest = new { name = "downloadtest", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);
        
        string content = "download this content";
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent(content), "file", "download.txt");
        form.Add(new StringContent("downloadtest"), "folderPath");
        await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Act - download file from folder
        HttpResponseMessage response = await _client.GetAsync("/files/download?filename=download.txt&folder=downloadtest", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        string downloaded = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(content, downloaded);
    }

    [Fact]
    public async Task DeleteFile_FromFolder_WorksCorrectly()
    {
        // Arrange - create folder and upload file
        var folderRequest = new { name = "deletetest", parentFolder = (string?)null };
        await _client.PostAsJsonAsync("/files/folder", folderRequest, TestContext.Current.CancellationToken);
        
        MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("delete me"), "file", "todelete.txt");
        form.Add(new StringContent("deletetest"), "folderPath");
        await _client.PostAsync("/files/upload", form, TestContext.Current.CancellationToken);

        // Act - delete file from folder
        HttpResponseMessage response = await _client.DeleteAsync("/files/file?filename=todelete.txt&folder=deletetest", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify file is gone but folder remains
        HttpResponseMessage listResponse = await _client.GetAsync("/files?folder=deletetest", TestContext.Current.CancellationToken);
        JsonElement listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(listJson.TryGetProperty("items", out JsonElement itemsElement));
        var items = itemsElement.EnumerateArray().ToList();
        Assert.Empty(items); // No files in folder
        
        // But folder should still exist in root
        HttpResponseMessage rootListResponse = await _client.GetAsync("/files", TestContext.Current.CancellationToken);
        JsonElement rootListJson = await rootListResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(rootListJson.TryGetProperty("items", out JsonElement rootItemsElement));
        var rootItems = rootItemsElement.EnumerateArray().ToList();
        var folderNames = rootItems.Where(item => item.GetProperty("type").GetString() == "folder")
                                  .Select(item => item.GetProperty("name").GetString())
                                  .ToList();
        Assert.Contains("deletetest", folderNames);
    }

    #endregion
}
