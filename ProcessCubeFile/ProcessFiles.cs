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
        private static string _connectionString = Environment.GetEnvironmentVariable("File_Share_Connection");
        
        [FunctionName("ProcessFiles")]
        public static async Task RunAsync([TimerTrigger("%Job_Schedule%")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            await TraverseAsync(_connectionString, _shareName, log);
        }

        private static async Task TraverseAsync(string connectionString, string shareName, ILogger log)
        {
            ShareClient share = new ShareClient(connectionString, shareName);

            // Get the root directory
            var root = share.GetRootDirectoryClient();

            // Traverse each item in the root directory
            await foreach (ShareFileItem item in root.GetFilesAndDirectoriesAsync())
            {
                // Print the name of the item
                log.LogInformation(item.Name);

                // If the item is our input folder then traverse it
                if (item.IsDirectory && item.Name == _inputFolder)
                {
                    var subClient = root.GetSubdirectoryClient(item.Name);
                    await foreach (ShareFileItem inputItem in subClient.GetFilesAndDirectoriesAsync())
                    {
                        if (!inputItem.IsDirectory)
                        {
                            var fileProcessor = new FileProcessor(connectionString, shareName, subClient.Name, inputItem.Name, _pattern, _outputFolder, log);
                            await fileProcessor.ProcessFile();
                        }
                    }
                }
            }
        }        
    }
}
