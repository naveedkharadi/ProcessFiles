# ProcessFiles

This is a simple project that implements the following task:

The task is to process a list of text files uploaded into an Azure file share* by users on daily basis**. 
It is required to run a pattern*** for each text line of the file and if any matched then the file needs to be moved into a separate output folder or else it deleted from its original location.

*Azure file share opposed to a blob.

**Assume there would be 1000's of files made available daily and the size could range from small to big ~500MB.

***Pattern must be configuration and is a string that contain letters, numbers, ? and * symbols. ? stands for 1 any character, * stands for 0 or many of any characters. For instance, input 'abcd' matches pattern 'a*d' but input 'abcde' doesn't.

## Assumptions / Clarifications / Understanding

- An Azure subscription is available with:
  - A storage account
    - Having a File Share configured such that there are 2 folders (the names of folders are configurable via app settings)
      - Input: Incoming location where the files could be dropped
      - Output: Where the files will be moved if the pattern is matched
- The deployment of this code will create a Timer triggered function called ProcessFiles
  - The timer's trigger value is also configurable and follows CRON style of scheduling

## Configurations

Following items will need to be there in the app settings / configuration:
- File_Share: Name of the file share in Storage Account
- File_Share_Connection: Connection string to connect to the File Share (this can be obtained from the Storage Account's Access Keys blade in Azure Portal)
- Input_Folder: Name of the folder/sub-directory in the File Share from where the Function App will read the files (i.e. source location)
- Output_Folder: Name of the folder/sub-directory in the File Share where the Function App will move the files (i.e. destination location)
- Job_Schedule: Currently set to */55 * * * * * [uses CRON syntax]
- Pattern: A regex pattern to match for in the lines of text in the incoming files. Currently set to M[a-zA-Z0-9]*n$ (indicating that it will match any line that starts with an 'M' and ends with an 'n' such that in between there are any alphabets or numbers.

