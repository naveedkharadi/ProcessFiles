using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Sas;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProcessCubeFile
{
    public static class ProcessFiles
    {
        private static string _pattern = Environment.GetEnvironmentVariable("Pattern");
        private static string _shareName = Environment.GetEnvironmentVariable("File_Share");
        private static string _inputFolder = Environment.GetEnvironmentVariable("Input_Folder");
        private static string _outputFolder = Environment.GetEnvironmentVariable("Output_Folder");
        private static string _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        [FunctionName("ProcessFiles")]
        public static async Task RunAsync([TimerTrigger("%Job_Schedule%")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            await TraverseAsync(_connectionString, _shareName);
        }

        private static async Task TraverseAsync(string connectionString, string shareName)
        {
            ShareClient share = new ShareClient(connectionString, shareName);

            // Get the root directory
            var root = share.GetRootDirectoryClient();

            // Traverse each item in the root directory
            await foreach (ShareFileItem item in root.GetFilesAndDirectoriesAsync())
            {
                // Print the name of the item
                Log(item.Name);

                // If the item is our input folder then traverse it
                if (item.IsDirectory && item.Name == _inputFolder)
                {
                    var subClient = root.GetSubdirectoryClient(item.Name);
                    await foreach (ShareFileItem inputItem in subClient.GetFilesAndDirectoriesAsync())
                    {
                        if (!inputItem.IsDirectory)
                        {
                            await ProcessFile(connectionString, shareName, subClient.Name, inputItem.Name);
                        }
                    }
                }
            }
        }

        private static async Task ProcessFile(string connectionString, string shareName, string dirName, string fileName)
        {
            (var share, var directory, var file) = GetFileShareClients(connectionString, shareName, dirName, fileName);
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
                    if(match != null && match.Success)
                    {
                        Log($"Pattern {_pattern} matched: {match}");
                        matchFound = true;
                        break;
                    }
                }
            }
            if(matchFound)
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
            catch(Exception ex)
            {
                Log(ex);
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine(message);
        }

        private static void Log(Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}
