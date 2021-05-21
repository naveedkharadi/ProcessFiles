using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProcessCubeFile
{
    public class FileProcessor
    {
        private static ILogger _logger;
        private string _pattern;
        private string _outputFolder;
        private string _connectionString;
        private string _shareName;
        private string _dirName; 
        private string _fileName;

        public FileProcessor(string connectionString, string shareName, string dirName, string fileName, string pattern, string outputFolder, ILogger log)
        {
            _connectionString = connectionString;
            _shareName = shareName;
            _dirName = dirName;
            _fileName = fileName;
            _pattern = pattern;
            _outputFolder = outputFolder;
            _logger = log;
        }

        public async Task ProcessFile()
        {
            (var share, var directory, var file) = GetFileShareClients(_connectionString, _shareName, _dirName, _fileName);
            Stopwatch stopwatch = new Stopwatch();
            ShareFileDownloadInfo download = await file.DownloadAsync();
            bool matchFound = false;
            Log($"Processing file: {file.Name} -> Size: {download.ContentLength} bytes.");
            stopwatch.Start();
            using (Stream fs = download.Content)
            using (BufferedStream bs = new BufferedStream(fs))
            using (StreamReader sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var match = Regex.Match(line, _pattern);
                    if (match != null && match.Success)
                    {
                        Log($"Pattern {_pattern} matched: {match}");
                        matchFound = true;
                        break;
                    }
                }
            }
            if (matchFound)
            {
                stopwatch.Stop();
                Log($"Pattern matched - moving {file.Uri} to {_outputFolder} in the File Share.");
                Log($"Pattern mateched in: {stopwatch.ElapsedMilliseconds} milliseconds.");
                await MoveFileAsync(share, directory, file, _outputFolder);
            }
            else
            {
                stopwatch.Stop();
                Log($"Pattern NOT matched - deleting {file.Uri}.");
                Log($"Time taken to read the file: {stopwatch.ElapsedMilliseconds} milliseconds.");
                await DeleteFileAsync(file);
            }
            stopwatch.Reset();
        }

        private static (ShareClient, ShareDirectoryClient, ShareFileClient) GetFileShareClients(string connectionString, string shareName, string dirName, string fileName)
        {
            ShareClient share = new ShareClient(connectionString, shareName);
            ShareDirectoryClient directory = share.GetDirectoryClient(dirName);
            ShareFileClient file = directory.GetFileClient(fileName);

            return (share, directory, file);
        }

        private static async Task DeleteFileAsync(ShareFileClient file)
        {
            try
            {
                if (await file.DeleteIfExistsAsync())
                {
                    Log($"Deleted {file.Uri}.");
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private static async Task MoveFileAsync(ShareClient share, ShareDirectoryClient directory, ShareFileClient file, string outputFolder)
        {
            try
            {
                ShareDirectoryClient outputDirectory = share.GetDirectoryClient(outputFolder);

                await outputDirectory.CreateIfNotExistsAsync();
                if (outputDirectory.Exists())
                {
                    ShareFileClient outputFile = outputDirectory.GetFileClient(file.Name);
                    await outputFile.StartCopyAsync(file.Uri);

                    if (await outputFile.ExistsAsync())
                    {
                        Log($"{file.Uri} copied to {outputFile.Uri}");
                        await DeleteFileAsync(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private static void Log(string message)
        {
            _logger.LogInformation(message);
        }

        private static void Log(Exception exception)
        {
            _logger.LogError(exception, "Exception!");
        }
    }
}
